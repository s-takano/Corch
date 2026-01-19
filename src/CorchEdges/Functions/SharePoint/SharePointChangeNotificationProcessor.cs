using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CorchEdges.Abstractions;
using CorchEdges.Models;
using CorchEdges.Services;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Azure.Messaging.ServiceBus;

namespace CorchEdges.Functions.SharePoint;

// ─────────────────────────────────────────────────────────────────────────────
//  Azure Function – thin, DI‑friendly wrapper
// ─────────────────────────────────────────────────────────────────────────────
/// <summary>
/// Represents an Azure Function responsible for processing SharePoint change notifications.
/// </summary>
/// <remarks>
/// The function listens to messages from a Service Bus queue and processes SharePoint change events.
/// It verifies the Microsoft Graph API connection, processes the notification messages,
/// and handles errors such as failures in processing or connectivity issues.
/// </remarks>
public sealed class SharePointChangeNotificationProcessor
{
    /// <summary>
    /// A logger instance used for logging information, warnings, errors, and debug-level messages
    /// within the <see cref="SharePointChangeNotificationProcessor"/> class to provide detailed traceability
    /// of the SharePoint change processing workflow.
    /// </summary>
    /// <remarks>
    /// This logger is specifically implemented for the <see cref="SharePointChangeNotificationProcessor"/> class,
    /// and it facilitates monitoring and troubleshooting by recording critical processing steps,
    /// errors, and additional context regarding Azure Function execution and external system interactions.
    /// </remarks>
    private readonly ILogger<SharePointChangeNotificationProcessor> _log;

    /// <summary>
    /// Represents a private instance of the <see cref="SharePointSyncProcessor"/> class.
    /// Responsible for handling changes and processing notifications related to SharePoint changes.
    /// </summary>
    private readonly ISharePointSyncProcessor _processor;

    /// <summary>
    /// Represents a BlobContainerClient instance used to store failed SharePoint change notifications.
    /// This container holds message data for manual retry or analysis in cases where processing fails
    /// (e.g., connectivity issues, unhandled errors, etc.).
    /// </summary>
    private readonly BlobContainerClient _failedContainer;

    private sealed record ContinuationPayload(IReadOnlyList<string> ItemIds, string DeltaLink);

    private readonly ServiceBusSender _sender;
    private const int BatchSize = 200;

    /// <summary>
    /// Represents an Azure Function for processing SharePoint change notifications
    /// received through a Service Bus queue. This class handles deserialization
    /// of the notification message, verifies Graph API connection, processes changes,
    /// and logs or persists failed messages to a Blob storage for further analysis.
    /// </summary>
    /// <remarks>
    /// This class is designed to serve as a thin, dependency injection (DI)-friendly
    /// wrapper leveraging Azure functions for triggering operations. It integrates
    /// with supporting services to ensure robust handling of SharePoint change events.
    /// </remarks>
    public SharePointChangeNotificationProcessor(ILogger<SharePointChangeNotificationProcessor> log,
        ISharePointSyncProcessor processor, BlobServiceClient blobs, ServiceBusClient bus)
    {
        _log = log;
        _processor = processor;
        _failedContainer = blobs.GetBlobContainerClient("failed-changes");
        _sender = bus.CreateSender("sp-changes");

        // Ensure the container exists (async fire-and-forget is fine for this)
        _ = Task.Run(async () =>
        {
            try
            {
                await _failedContainer.CreateIfNotExistsAsync(PublicAccessType.None);
            }
            catch (Exception ex)
            {
                // Log error but don't fail startup
                Console.WriteLine($"Failed to create container: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Processes a message received from the Service Bus, verifies the Graph API connection,
    /// and handles SharePoint change notifications.
    /// </summary>
    /// <param name="msg">The serialized SharePoint change notification message received from Service Bus.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Function("ProcessSharePointNotification")]
    public async Task<SharePointSyncResult> ProcessNotificationAsync(
        [ServiceBusTrigger("sp-changes", Connection = "ServiceBusConnection")]
        string msg)
    {
        try
        {
            // 🔍 Step 1: Verify Graph connection before processing
            _log.LogInformation("Verifying Graph API connection...");

            if (!await _processor.EnsureGraphConnectionAsync())
            {
                _log.LogError("Graph API connection failed - aborting message processing");

                var blob = await SaveFailedMessageToBlob("graph-connection-failed", msg);
                _log.LogWarning("Message saved to {blob} for retry when Graph connection is restored", blob);

                // Don't throw - this prevents infinite retries for credential issues
                // The message is safely stored in blob storage for manual processing
                return SharePointSyncResult.Failed("Can't connect to Graph API");
            }

            // 📨 Process the notification message
            _log.LogInformation("Deserializing SharePoint change notification...");

            SharePointSyncResult result;

            if (IsNotificationEnvelope(msg))
            {
                var env = JsonSerializer.Deserialize<NotificationEnvelope>(
                    msg, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                _log.LogInformation("Processing {count} change notifications", env!.Value.Length);
                if (env.Value.Length == 0) return SharePointSyncResult.Succeeded();
                result = await _processor.FetchAndStoreDeltaAsync(BatchSize);
            }
            else if (TryDeserialize<ContinuationPayload>(msg, out var continuation) &&
                     continuation?.ItemIds is { Count: > 0 } &&
                     !string.IsNullOrEmpty(continuation.DeltaLink))
            {
                result = await _processor.FetchAndStoreItemsAsync(
                    continuation.ItemIds,
                    continuation.DeltaLink,
                    finalizeDeltaLink: continuation.ItemIds.Count <= BatchSize,
                    batchSize: BatchSize);
            }
            else
            {
                _log.LogError("Message is neither SharePoint notification nor continuation payload.");
                return SharePointSyncResult.Succeeded();
            }

            if (!result.Success)
            {
                var blog = await SaveFailedMessageToBlob("processing-error", msg);
                _log.LogError("Error processing change notification: {error}, {blob}", result.ErrorReason, blog);
                return SharePointSyncResult.Succeeded();
            }

            if (result.HasMoreWork && result?.PendingDeltaLink != null)
            {
                var payload = new ContinuationPayload(result.RemainingItemIds, result.PendingDeltaLink);
                var continuationMsg = JsonSerializer.Serialize(payload);
                await _sender.SendMessageAsync(new ServiceBusMessage(continuationMsg));

                _log.LogInformation("Enqueued continuation message with {count} items", result.RemainingItemIds.Count);
            }

            _log.LogInformation("Successfully processed batch");
            return SharePointSyncResult.Succeeded();
        }
        catch (Exception ex)
        {
            var blob = await SaveFailedMessageToBlob("processing-error", msg);
            _log.LogError(ex, "Unhandled error during message processing - saved to {blob}", blob);
            throw;
        }
    }

    private static bool IsNotificationEnvelope(string msg)
    {
        var isNotificationEnvelope = false;
        try
        {
            using var doc = JsonDocument.Parse(msg);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("value", out var valueProp) &&
                valueProp.ValueKind == JsonValueKind.Array)
            {
                isNotificationEnvelope = true;
            }
        }
        catch (Exception e)
        {
            throw new JsonException(e.Message);
        }

        return isNotificationEnvelope;
    }

    private static bool TryDeserialize<T>(string json, out T? result)
    {
        try
        {
            result = JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return result != null;
        }
        catch (JsonException)
        {
            result = default;
            return false;
        }
    }

    private async Task<string> SaveFailedMessageToBlob(string blobClass, string msg)
    {
        // Save the message to the failed blob for manual retry when the connection is restored
        var blob = blobClass + $"-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.json";
        await _failedContainer.UploadBlobAsync(blob, BinaryData.FromString(msg));
        return blob;
    }
}