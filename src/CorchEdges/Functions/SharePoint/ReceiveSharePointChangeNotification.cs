using System.Net;
using CorchEdges.Abstractions;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace CorchEdges.Functions.SharePoint;

/// <summary>
/// Azure Function that handles SharePoint list change webhook notifications. This function receives
/// notifications when items in a specific SharePoint list are created, updated, or deleted.
/// It processes the validation handshake during subscription creation and enqueues change 
/// notifications to a Service Bus queue for asynchronous processing.
/// </summary>
/// <remarks>
/// The webhook subscription is configured to monitor a specific SharePoint list using the resource pattern:
/// "/sites/{siteId}/lists/{listId}". This ensures only relevant list item changes trigger the function,
/// providing efficient and focused notification processing.
/// 
/// The function handles two main scenarios:
/// 1. Validation handshake - Returns the validation token during subscription setup
/// 2. Change notifications - Enqueues notification data for downstream processing
/// 
/// <para><strong>Future Architecture Consideration:</strong></para>
/// This function is currently designed for list-specific notifications. When expanding to handle
/// multiple SharePoint resource types (document libraries, sites, etc.), consider implementing
/// a router pattern:
/// <code>
/// Current: SharePoint List → ReceiveSharePointChangeNotification → SharePointSyncFunction
/// Future:  SharePoint Site → ReceiveSharePointChangeNotification → Router → [ListProcessor, LibraryProcessor, SiteProcessor]
/// </code>
/// The router would examine the notification resource type and route to appropriate specialized processors,
/// allowing this function to become a generic entry point while maintaining separation of concerns.
/// </remarks>
public sealed class ReceiveSharePointChangeNotification(IWebhookProcessor svc, ILogger<ReceiveSharePointChangeNotification> logger)
{
    /// <summary>
    /// Represents the output bindings for the SharePoint webhook function.
    /// Combines both Service Bus message output and HTTP response in a single immutable record.
    /// </summary>
    /// <param name="BusMessage">
    /// Optional JSON message to be sent to the "sp-changes" Service Bus queue.
    /// Contains serialized notification data when a change notification is received.
    /// Null during validation handshake or error scenarios.
    /// </param>
    /// <param name="HttpResponse">
    /// HTTP response to be returned to SharePoint. Contains the validation token during handshake
    /// or acknowledgment response for change notifications.
    /// </param>
    public record Out(
        [property: ServiceBusOutput("sp-changes",
            Connection = "ServiceBusConnection")]
        string? BusMessage,
        HttpResponseData HttpResponse);

    /// <summary>
    /// Processes incoming SharePoint webhook requests for list change notifications.
    /// Handles both validation handshakes during subscription creation and actual change notifications.
    /// </summary>
    /// <param name="req">
    /// The HTTP request from SharePoint containing either a validation token (for handshake)
    /// or notification payload (for list changes). The request method can be GET or POST.
    /// </param>
    /// <returns>
    /// A record containing:
    /// - BusMessage: JSON string for Service Bus queue (null for handshakes or errors)
    /// - HttpResponse: HTTP response for SharePoint (validation token or acknowledgment)
    /// </returns>
    /// <remarks>
    /// This function is triggered by SharePoint when:
    /// 1. Initial subscription validation (handshake) - receives validation token
    /// 2. List item changes - receives notification with change details
    /// 
    /// All change notifications are enqueued to Service Bus for reliable asynchronous processing
    /// by the SharePointSyncFunction function.
    /// </remarks>
    [Function(nameof(ReceiveSharePointChangeNotification))]
    public async Task<Out> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "sharepoint/webhook")]
        HttpRequestData req)
    {
        // Handle validation handshake during subscription creation
        logger.LogInformation("Processing webhook request - checking for handshake...");

        if (svc.TryHandshake(req) is { } hs)
        {
            logger.LogInformation("Handshake successful - returning validation response");
            logger.LogDebug("Handshake response status: {StatusCode}", hs.StatusCode);
            return new Out(null, hs);
        }

        logger.LogInformation("No handshake detected - processing as notification");

        // Process change notification and enqueue for asynchronous processing
        logger.LogInformation("Building response and enqueuing payload...");

        try
        {
            var (resp, msg) = await svc.BuildEnqueueAsync(req);
            
            logger.LogInformation("BuildEnqueueAsync completed successfully");
            logger.LogDebug("Response created: {HasResponse}", resp != null);
            logger.LogDebug("Message created: {HasMessage}", msg != null);
            
            if (msg != null)
            {
                logger.LogInformation("Message enqueued for processing - Type: {MessageType}", msg.GetType().Name);
            }
            else
            {
                logger.LogWarning("No message was created during BuildEnqueueAsync");
            }
            
            // Ensure we always return a valid HTTP response to SharePoint
            if (resp == null)
            {
                logger.LogInformation("No response from BuildEnqueueAsync - creating default OK response");
                resp = req.CreateResponse(HttpStatusCode.OK);
            }
            else
            {
                logger.LogInformation("Using response from BuildEnqueueAsync - Status: {StatusCode}", resp.StatusCode);
            }

            logger.LogInformation("Webhook processing completed successfully");
            return new Out(msg, resp);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during BuildEnqueueAsync: {Message}", ex.Message);
            
            // Create error response but still return 200 OK to prevent SharePoint retries
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            logger.LogWarning("Created error response due to exception");
            
            return new Out(null, errorResponse);
        }
    }
}