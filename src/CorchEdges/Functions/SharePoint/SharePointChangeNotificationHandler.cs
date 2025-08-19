using System.Net;
using CorchEdges.Abstractions;
using CorchEdges.Models;
using CorchEdges.Models.Response;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;

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
/// Current: SharePoint List → SharePointChangeNotificationHandler → SharePointChangeNotificationProcessor
/// Future:  SharePoint Site → SharePointChangeNotificationHandler → Router → [ListProcessor, LibraryProcessor, SiteProcessor]
/// </code>
/// The router would examine the notification resource type and route to appropriate specialized processors,
/// allowing this function to become a generic entry point while maintaining separation of concerns.
/// </remarks>
public sealed class SharePointChangeNotificationHandler(ISharePointWebhookProcessor svc, ILogger<SharePointChangeNotificationHandler> logger)
{
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
    /// by the SharePointChangeNotificationProcessor function.
    /// </remarks>
    [Function("HandleSharePointChangeNotification")]
    [OpenApiOperation(
        operationId: "SharePointChangeNotificationHandler", 
        tags: new[] { "SharePoint Webhooks" },
        Summary = "Receive SharePoint change notifications",
        Description = "Webhook endpoint that receives SharePoint list change notifications. Handles both validation handshakes during subscription setup and actual change notifications. Change notifications are automatically enqueued to Service Bus for asynchronous processing.")]
    [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
    
    // GET operation for validation handshake
    [OpenApiParameter(
        name: "validationToken", 
        In = ParameterLocation.Query, 
        Required = false, 
        Type = typeof(string),
        Description = "SharePoint validation token sent during subscription creation handshake")]
    
    // POST operation for change notifications
    [OpenApiRequestBody(
        contentType: "application/json", 
        bodyType: typeof(NotificationEnvelope),
        Required = false,
        Description = "SharePoint change notification payload containing array of change notifications")]
    
    // Success responses
    [OpenApiResponseWithBody(
        statusCode: HttpStatusCode.OK, 
        contentType: "text/plain", 
        bodyType: typeof(string), 
        Description = "Validation token echo (for handshake) or notification acknowledgment")]
    
    // Error responses
    [OpenApiResponseWithoutBody(
        statusCode: HttpStatusCode.BadRequest, 
        Description = "Invalid request format or missing required parameters")]
    [OpenApiResponseWithoutBody(
        statusCode: HttpStatusCode.InternalServerError, 
        Description = "Internal processing error - notification may be retried by SharePoint")]
    public async Task<SharePointChangeNotificationResponse> HandleNotificationAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "sharepoint/notifications")]
        HttpRequestData req)
    {
        // Handle validation handshake during subscription creation
        logger.LogInformation("Processing webhook request - checking for handshake...");

        if (svc.TryHandshake(req) is { } hs)
        {
            logger.LogInformation("Handshake successful - returning validation response");
            logger.LogDebug("Handshake response status: {StatusCode}", hs.StatusCode);
            return new SharePointChangeNotificationResponse(null, hs);
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
            return new SharePointChangeNotificationResponse(msg, resp);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during BuildEnqueueAsync: {Message}", ex.Message);
            
            // Create error response but still return 200 OK to prevent SharePoint retries
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            logger.LogWarning("Created error response due to exception");
            
            return new SharePointChangeNotificationResponse(null, errorResponse);
        }
    }
}