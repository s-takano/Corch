// -----------------------------------------------------------------------------
//  SharePointWebhookCallback.cs – refactored for unit‑testability
// -----------------------------------------------------------------------------

using System.Net;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace CorchEdges;

// ─────────────────────────────────────────────────────────────────────────────
//  Pure business logic (no Azure types except HttpRequest/Response)
// ─────────────────────────────────────────────────────────────────────────────
public interface IWebhookProcessor
{
    /// <summary>Returns a handshake response or <c>null</c> when not handshake.</summary>
    HttpResponseData? TryHandshake(HttpRequestData req);

    /// <summary>Returns (202 Accepted, queue payload) or (error, null).</summary>
    Task<(HttpResponseData response, string? queueBody)> BuildEnqueueAsync(HttpRequestData req);
}

public sealed class DefaultWebhookProcessor(ILogger<DefaultWebhookProcessor> log) : IWebhookProcessor
{
    private readonly ILogger _log = log ?? throw new ArgumentNullException(nameof(log));

    public HttpResponseData? TryHandshake(HttpRequestData req)
    {
        var q = QueryHelpers.ParseQuery(req.Url.Query);
        if (q.TryGetValue("validationtoken", out StringValues v) || q.TryGetValue("validationToken", out v))
        {
            var ok = req.CreateResponse(HttpStatusCode.OK);
            ok.WriteStringAsync(v.ToString(), Encoding.UTF8).GetAwaiter().GetResult();
            _log.LogInformation("Validation handshake responded.");
            return ok;
        }

        return null;
    }

    public async Task<(HttpResponseData response, string? queueBody)> BuildEnqueueAsync(HttpRequestData req)
    {
        string? body = await req.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(body))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Empty body");
            return (bad, null);
        }

        var acc = req.CreateResponse(HttpStatusCode.Accepted);
        await acc.WriteStringAsync("Queued.");
        _log.LogInformation("Notification ({len} bytes) queued.", body.Length);
        return (acc, body);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  Azure Function – thin wrapper delegates to IWebhookProcessor
// ─────────────────────────────────────────────────────────────────────────────
public sealed class SharePointWebhookCallback
{
    private readonly IWebhookProcessor _svc;
    public SharePointWebhookCallback(IWebhookProcessor svc) => _svc = svc;

    [Function(nameof(SharePointWebhookCallback))]
    [ServiceBusOutput("sp-changes", Connection = "ServiceBusConnection")]
    public async Task<(HttpResponseData http, string? bus)> Run([
            HttpTrigger(AuthorizationLevel.Function, "get", "post")]
        HttpRequestData req)
    {
        // 1) handshake short‑circuit
        if (_svc.TryHandshake(req) is HttpResponseData hs)
            return (hs, null);

        // 2) otherwise enqueue notification
        return await _svc.BuildEnqueueAsync(req);
    }
}