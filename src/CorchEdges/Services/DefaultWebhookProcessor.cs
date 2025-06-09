// -----------------------------------------------------------------------------
//  SharePointWebhookCallback.cs – refactored for unit‑testability
// -----------------------------------------------------------------------------

using System.Net;
using System.Text;
using CorchEdges.Abstractions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace CorchEdges.Services;

// ─────────────────────────────────────────────────────────────────────────────
//  Pure business logic (no Azure types except HttpRequest/Response)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// A default implementation of the <see cref="IWebhookProcessor"/> interface used to handle webhook requests.
/// </summary>
/// <remarks>
/// The <c>DefaultWebhookProcessor</c> class provides two main functionalities:
/// - Handling and responding to validation handshake requests.
/// - Building and queuing notification payloads.
/// This class relies on an <see cref="ILogger{TCategoryName}"/> for logging important information during the execution.
/// </remarks>
public sealed class DefaultWebhookProcessor(ILogger<DefaultWebhookProcessor> log) : IWebhookProcessor
{
    /// <summary>
    /// Represents the logger instance used for logging information, warnings, errors, and other messages
    /// within the <see cref="DefaultWebhookProcessor"/> class. This instance facilitates structured logging.
    /// </summary>
    private readonly ILogger _log = log ?? throw new ArgumentNullException(nameof(log));

    /// Attempts to perform a handshake by responding to a validation token if present in the query string of the HTTP request.
    /// <param name="req">The HTTP request containing the query string to check for a validation token.</param>
    /// <returns>
    /// An HTTP response with a status of 200 (OK) and the validation token as the body, if the token is present and valid.
    /// Returns null if the validation token is not found in the query string.
    /// </returns>
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

    /// Asynchronously processes the incoming HTTP request and constructs a response
    /// based on the validity of the request's body. It also returns the body of
    /// the request for potential queuing purposes.
    /// <param name="req">The incoming HTTP request containing the body to process.</param>
    /// <returns>
    /// A tuple where the first item is an <see cref="HttpResponseData"/> representing the HTTP response,
    /// and the second item is a nullable string representing the body of the request if it is valid;
    /// otherwise, null.
    /// </returns>
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
