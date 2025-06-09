using CorchEdges.Abstractions;
using Microsoft.Azure.Functions.Worker.Http;

namespace CorchEdges.Functions;


// ─────────────────────────────────────────────────────────────────────────────
//  Azure Function – thin wrapper delegates to IWebhookProcessor
// ─────────────────────────────────────────────────────────────────────────────
/// <summary>
/// Represents an Azure Function callback handler for SharePoint webhook callbacks.
/// </summary>
/// <remarks>
/// This class acts as a thin wrapper that delegates webhook handling tasks to an
/// implementation of the <see cref="IWebhookProcessor"/> interface. It is triggered
/// by incoming HTTP requests and can enqueue messages to an Azure Service Bus.
/// </remarks>
public sealed class SharePointWebhookCallback(IWebhookProcessor svc)
{
    /// <summary>
    /// Handles an incoming HTTP request from a SharePoint webhook, either
    /// completing a handshake or processing a notification and queuing it for further processing.
    /// </summary>
    /// <param name="req">
    /// The HTTP request data sent to the Azure Function. This includes the request method, headers, body, and other context information.
    /// </param>
    /// <returns>
    /// A tuple containing the HTTP response and an optional Service Bus message payload:
    /// - <c>http</c>: The HTTP response to return, indicating success or failure.
    /// - <c>bus</c>: The message to enqueue to the Service Bus, or null if no message should be enqueued.
    /// </returns>
    [Function(nameof(SharePointWebhookCallback))]
    [ServiceBusOutput("sp-changes", Connection = "ServiceBusConnection")]
    public async Task<(HttpResponseData http, string? bus)> Run([
            HttpTrigger(AuthorizationLevel.Function, "get", "post")]
        HttpRequestData req)
    {
        // 1) handshake short‑circuit
        if (svc.TryHandshake(req) is HttpResponseData hs)
            return (hs, null);

        // 2) otherwise enqueue notification
        return await svc.BuildEnqueueAsync(req);
    }
}