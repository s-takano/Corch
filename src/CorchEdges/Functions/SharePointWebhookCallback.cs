using CorchEdges.Abstractions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace CorchEdges.Functions;

/// <summary>
/// Represents an Azure Function designed to process SharePoint webhook callbacks. This function
/// handles the webhook handshake process with SharePoint or places the incoming notification
/// into a Service Bus queue. It leverages a multiple-output object for returning both
/// Service Bus messages and HTTP responses.
/// </summary>
public sealed class SharePointWebhookCallback(IWebhookProcessor svc, ILogger<SharePointWebhookCallback> logger)
{
    // immutable record whose properties carry output bindings
    /// <summary>
    /// Represents an immutable record used as the output type for the SharePointWebhookCallback function.
    /// Encapsulates the output bindings, including a Service Bus message and an HTTP response.
    /// </summary>
    public record Out(
        [property: ServiceBusOutput("sp-changes",
            Connection = "ServiceBusConnection")]
        string? BusMessage,
        HttpResponseData HttpResponse);

    /// <summary>
    /// Processes an incoming HTTP request for a SharePoint webhook and handles the handshake or enqueues a message for further processing.
    /// </summary>
    /// <param name="req">The HTTP request data received by the function, which includes details of the SharePoint webhook event.</param>
    /// <returns>
    /// A record containing the HTTP response and an optional message to enqueue into a Service Bus queue.
    /// The HTTP response is always valid and provides the appropriate status or handshake response.
    /// If a message needs to be enqueued, it is returned in the BusMessage property; otherwise, it is null.
    /// </returns>
    [Function(nameof(SharePointWebhookCallback))]
    public async Task<Out> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "sharepoint/webhook")]
        HttpRequestData req)
    {
        // 1) handshake?
        logger.LogInformation("Processing webhook request - checking for handshake...");

        if (svc.TryHandshake(req) is { } hs)
        {
            logger.LogInformation("Handshake successful - returning validation response");
            logger.LogDebug("Handshake response status: {StatusCode}", hs.StatusCode);
            return new Out(null, hs);
        }

        logger.LogInformation("No handshake detected - processing as notification");

        // 2) build HTTP reply and queue payload
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
            
            // ensure we always return a valid HTTP response
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
            
            // Create error response
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            logger.LogWarning("Created error response due to exception");
            
            return new Out(null, errorResponse);
        }
    }
}