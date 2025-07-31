using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CorchEdges.Abstractions;
using CorchEdges.Models;
using CorchEdges.Services;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;

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
public sealed class SharePointSyncFunction
{
    /// <summary>
    /// A logger instance used for logging information, warnings, errors, and debug-level messages
    /// within the <see cref="SharePointSyncFunction"/> class to provide detailed traceability
    /// of the SharePoint change processing workflow.
    /// </summary>
    /// <remarks>
    /// This logger is specifically implemented for the <see cref="SharePointSyncFunction"/> class,
    /// and it facilitates monitoring and troubleshooting by recording critical processing steps,
    /// errors, and additional context regarding Azure Function execution and external system interactions.
    /// </remarks>
    private readonly ILogger<SharePointSyncFunction> _log;

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
    internal SharePointSyncFunction(ILogger<SharePointSyncFunction> log, ISharePointSyncProcessor processor, BlobServiceClient blobs)
    {
        _log = log; 
        _processor = processor;
        _failedContainer = blobs.GetBlobContainerClient("failed-changes");
        
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
    [Function(nameof(SharePointSyncFunction))]
    public async Task<SharePointSyncResult> RunAsync([ServiceBusTrigger("sp-changes", Connection="ServiceBusConnection")] string msg)
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
            
            var env = JsonSerializer.Deserialize<NotificationEnvelope>(msg, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            
            _log.LogInformation("Processing {count} change notifications", env.Value.Length);

            foreach (var notification in env.Value.ToList())
            {
                _log.LogDebug("Processing change notification for resource: {resource}", notification.Resource);
                var result = await _processor.FetchAndStoreDeltaAsync();
                if (!result.Success)
                {
                    var blog = await SaveFailedMessageToBlob("processing-error", msg);
                    _log.LogError("Error processing change notification: {error}, {blob}", result.ErrorReason, blog);
                    break;
                }
                _log.LogInformation("Successfully processed {count} notifications", _processor.SuccessfulItems);
            }

            _log.LogInformation("Successfully processed all change notifications");
            
            return SharePointSyncResult.Succeeded();
        }
        catch (Exception ex)
        {
            // 💾 Save failed message for analysis and potential retry
            var blob = await SaveFailedMessageToBlob("processing-error", msg);
            _log.LogError(ex, "Unhandled error during message processing - saved to {blob}", blob);
            
            // Re-throw to trigger Service Bus retry logic
            throw;
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