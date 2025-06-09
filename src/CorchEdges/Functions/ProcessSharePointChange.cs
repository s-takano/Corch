using System.Text.Json;
using Azure.Storage.Blobs;
using CorchEdges.Models;
using Microsoft.Extensions.Logging;

namespace CorchEdges.Functions;

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
public sealed class ProcessSharePointChange
{
    /// <summary>
    /// A logger instance used for logging information, warnings, errors, and debug-level messages
    /// within the <see cref="ProcessSharePointChange"/> class to provide detailed traceability
    /// of the SharePoint change processing workflow.
    /// </summary>
    /// <remarks>
    /// This logger is specifically implemented for the <see cref="ProcessSharePointChange"/> class,
    /// and it facilitates monitoring and troubleshooting by recording critical processing steps,
    /// errors, and additional context regarding Azure Function execution and external system interactions.
    /// </remarks>
    private readonly ILogger<ProcessSharePointChange> _log;

    /// <summary>
    /// Represents a private instance of the <see cref="SharePointChangeHandler"/> class.
    /// Responsible for handling changes and processing notifications related to SharePoint changes.
    /// </summary>
    private readonly SharePointChangeHandler _handler;

    /// <summary>
    /// Represents a BlobContainerClient instance used to store failed SharePoint change notifications.
    /// This container holds message data for manual retry or analysis in cases where processing fails
    /// (e.g., connectivity issues, unhandled errors, etc.).
    /// </summary>
    private readonly BlobContainerClient _failed;

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
    public ProcessSharePointChange(ILogger<ProcessSharePointChange> log, SharePointChangeHandler handler, BlobServiceClient blobs)
    {
        _log = log; 
        _handler = handler; 
        _failed = blobs.GetBlobContainerClient("failed-changes");
    }

    /// <summary>
    /// Processes a message received from the Service Bus, verifies the Graph API connection,
    /// and handles SharePoint change notifications.
    /// </summary>
    /// <param name="msg">The serialized SharePoint change notification message received from Service Bus.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Function(nameof(ProcessSharePointChange))]
    public async Task RunAsync([ServiceBusTrigger("sp-changes", Connection="ServiceBusConnection")] string msg)
    {
        try
        {
            // 🔍 Step 1: Verify Graph connection before processing
            _log.LogInformation("Verifying Graph API connection...");
            
            var connectionValid = await _handler.EnsureGraphConnectionAsync();
            if (!connectionValid)
            {
                _log.LogError("Graph API connection failed - aborting message processing");
                
                // Save the message to the failed blob for manual retry when the connection is restored
                var blob = $"graph-connection-failed-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.json";
                await _failed.UploadBlobAsync(blob, BinaryData.FromString(msg));
                _log.LogWarning("Message saved to {blob} for retry when Graph connection is restored", blob);
                
                // Don't throw - this prevents infinite retries for credential issues
                // The message is safely stored in blob storage for manual processing
                return;
            }

            // 📨 Step 2: Process the notification message
            _log.LogInformation("Processing SharePoint change notification...");
            
            var env = JsonSerializer.Deserialize<NotificationEnvelope>(msg, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            
            _log.LogInformation("Processing {count} change notifications", env.Value.Length);
            
            foreach (var ch in env.Value) 
            {
                _log.LogDebug("Processing change notification for resource: {resource}", ch.Resource);
                await _handler.HandleAsync(ch);
            }
            
            _log.LogInformation("Successfully processed all change notifications");
        }
        catch (Exception ex)
        {
            // 💾 Save failed message for analysis and potential retry
            string blob = $"processing-error-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.json";
            await _failed.UploadBlobAsync(blob, BinaryData.FromString(msg));
            _log.LogError(ex, "Unhandled error during message processing - saved to {blob}", blob);
            
            // Re-throw to trigger Service Bus retry logic
            throw;
        }
    }
}