using CorchEdges.Abstractions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Threading.Tasks;

namespace CorchEdges.Functions;

/// <summary>
/// Represents an Azure Function designed to process SharePoint webhook callbacks. This function
/// handles the webhook handshake process with SharePoint or places the incoming notification
/// into a Service Bus queue. It leverages a multiple-output object for returning both
/// Service Bus messages and HTTP responses.
/// </summary>
public sealed class SharePointWebhookCallback(IWebhookProcessor svc)
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
        if (svc.TryHandshake(req) is { } hs)
            return new Out(null, hs);

        // 2) build HTTP reply and queue payload
        var (resp, msg) = await svc.BuildEnqueueAsync(req);

        // ensure we always return a valid HTTP response
        resp ??= req.CreateResponse(HttpStatusCode.OK);

        return new Out(msg, resp);
    }
}