using CorchEdges.Abstractions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Threading.Tasks;

namespace CorchEdges.Functions;

/// <summary>
/// HTTP endpoint that completes the SharePoint handshake **or** enqueues the
/// notification on Service Bus. Uses the recommended “multiple-output object”
/// pattern for isolated worker.
/// </summary>
public sealed class SharePointWebhookCallback(IWebhookProcessor svc)
{
    // immutable record whose properties carry output bindings
    public record Out(
        [property: ServiceBusOutput("sp-changes",
            Connection = "ServiceBusConnection")]
        string? BusMessage,
        HttpResponseData HttpResponse);

    [Function(nameof(SharePointWebhookCallback))]
    public async Task<Out> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "sharepoint/webhook")]
        HttpRequestData req)
    {
        // 1) handshake?
        if (svc.TryHandshake(req) is HttpResponseData hs)
            return new Out(null, hs);

        // 2) build HTTP reply and queue payload
        var (resp, msg) = await svc.BuildEnqueueAsync(req);

        // ensure we always return a valid HTTP response
        resp ??= req.CreateResponse(HttpStatusCode.OK);

        return new Out(msg, resp);
    }
}