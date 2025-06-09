using Microsoft.Azure.Functions.Worker.Http;

namespace CorchEdges.Abstractions;

/// <summary>
/// Represents a processor that handles incoming webhook requests
/// and performs handshake verifications or enqueue operations.
/// </summary>
public interface IWebhookProcessor
{
    /// <summary>
    /// Returns a handshake response or <c>null</c> when the request is not a handshake.
    /// </summary>
    /// <param name="req">An instance of <see cref="HttpRequestData"/> representing the incoming HTTP request.</param>
    /// <returns>An instance of <see cref="HttpResponseData"/> containing the handshake response, or <c>null</c> if the request is not a handshake.</returns>
    HttpResponseData? TryHandshake(HttpRequestData req);

    /// <summary>
    /// Builds a payload for enqueueing and returns an HTTP response along with the serialized queue message body or null.
    /// </summary>
    /// <param name="req">The incoming HTTP request containing webhook data.</param>
    /// <returns>
    /// A tuple containing the HTTP response and the optional queue message body.
    /// The response indicates the operation result, while the queue body represents the serialized payload or is null if an error occurred.
    /// </returns>
    Task<(HttpResponseData response, string? queueBody)> BuildEnqueueAsync(HttpRequestData req);
}