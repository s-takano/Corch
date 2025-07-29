using System.Net;
using System.Text.Json;
using System.Web;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;

namespace CorchEdges.Functions.Testing;

/// <summary>
/// A class that handles webhook callbacks for test purposes.
/// </summary>
/// <remarks>
/// The TestWebhookCallback class is designed to process HTTP requests sent to a specified webhook endpoint.
/// It supports GET and POST methods and includes functionality for handling Microsoft Graph webhook validation
/// handshakes and test notifications. The primary entry point for handling requests is the Run method.
/// </remarks>
public class TestWebhookCallback
{
    /// <summary>
    /// Instance of <see cref="ILogger{TCategoryName}"/> for logging messages in the <see cref="TestWebhookCallback"/> class.
    /// </summary>
    /// <remarks>
    /// Used to log informational messages, warnings, and errors during the execution of the TestWebhookCallback function,
    /// including request handling and error handling processes.
    /// </remarks>
    private readonly ILogger<TestWebhookCallback> _logger;

    /// Represents an Azure Function endpoint that handles webhook callbacks from external systems such as Microsoft Graph.
    /// This class is designed to process webhooks for testing purposes, including handling validation handshakes and
    /// processing notification payloads. The webhook can be triggered using both GET and POST methods, depending on the use case.
    public TestWebhookCallback(ILogger<TestWebhookCallback> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// Handles the execution of the Azure Function for processing Microsoft Graph webhook callbacks.
    /// <param name="req">
    /// The incoming HTTP request data, triggered by the webhook. It includes information
    /// such as the HTTP method and the request body or query parameters.
    /// </param>
    /// <returns>
    /// An <see cref="HttpResponseData"/> object representing the HTTP response to be sent back to the caller.
    /// This response may include status codes such as OK, MethodNotAllowed, or InternalServerError,
    /// along with relevant response content.
    /// </returns>
    [Function("TestWebhookCallback")]
    [OpenApiOperation(
        operationId: "TestWebhookCallback",
        tags: new[] { "Testing", "Webhooks" },
        Summary = "Test webhook callback endpoint",
        Description = @"Testing endpoint that simulates webhook callback behavior for development and testing purposes. 
    This endpoint can be used to:
    - Test webhook handshake validation
    - Simulate SharePoint change notifications
    - Validate webhook processing logic")]
    [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
    [OpenApiRequestBody(
        contentType: "application/json",
        bodyType: typeof(object),
        Required = false,
        Description = "Optional test payload - can be handshake validation token or change notification")]
    [OpenApiResponseWithBody(
        statusCode: System.Net.HttpStatusCode.OK,
        contentType: "application/json",
        bodyType: typeof(object),
        Description = "Test response - may include validation token for handshake or acknowledgment for notifications")]
    [OpenApiResponseWithBody(
        statusCode: System.Net.HttpStatusCode.BadRequest,
        contentType: "application/json",
        bodyType: typeof(object),
        Description = "Invalid test request")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "test/webhook")]
        HttpRequestData req)
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        _logger.LogInformation("[{RequestId}] TestWebhookCallback triggered - Method: {Method}, URL: {Url}",
            requestId, req.Method, req.Url);

        try
        {
            // Handle Microsoft Graph webhook validation handshake
            // Graph may send the handshake as POST or GET
            if (HasValidationToken(req))
                return await HandleValidationHandshake(req, requestId);

            // Normal change notifications are always POST
            if (req.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
                return await HandleTestNotification(req, requestId);

            _logger.LogWarning("[{RequestId}] Unsupported method: {Method}", requestId, req.Method);
            var response = req.CreateResponse(HttpStatusCode.MethodNotAllowed);
            await response.WriteStringAsync("Method not allowed");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{RequestId}] Error in test webhook callback", requestId);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Internal server error");
            return errorResponse;
        }
    }

    /// Checks if the HTTP request contains a "validationToken" query parameter.
    /// <param name="req">The HTTP request to inspect for a validation token.</param>
    /// <return>True if the "validationToken" query parameter is present; otherwise, false.</return>
    private static bool HasValidationToken(HttpRequestData req)
    {
        var qs = HttpUtility.ParseQueryString(req.Url.Query);
        return !string.IsNullOrEmpty(qs["validationToken"]);
    }

    /// Handles the validation handshake required by Microsoft Graph webhook subscriptions.
    /// <param name="req">The HTTP request data containing the validation token in the query string.</param>
    /// <param name="requestId">A unique identifier for the ongoing request, used for logging.</param>
    /// <returns>Returns an HTTP response containing the validation token in plain text, or an error response in case of invalid input or exceptions.</returns>
    private async Task<HttpResponseData> HandleValidationHandshake(HttpRequestData req, string requestId)
    {
        try
        {
            var queryParams = HttpUtility.ParseQueryString(req.Url.Query);
            var validationToken = queryParams["validationToken"];

            if (string.IsNullOrEmpty(validationToken))
            {
                _logger.LogWarning("[{RequestId}] Missing validationToken in handshake", requestId);
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Missing validationToken parameter");
                return badResponse;
            }

            _logger.LogInformation("[{RequestId}] ✅ Webhook handshake successful! Token: {TokenPreview}...",
                requestId, validationToken[..Math.Min(10, validationToken.Length)]);

            // Return validation token as plain text (required by Microsoft Graph)
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            // Use WriteStringAsync instead of WriteString
            await response.WriteStringAsync(validationToken);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{RequestId}] Error during validation handshake", requestId);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Handshake failed");
            return errorResponse;
        }
    }

    /// <summary>
    /// Handles the test notification received from the webhook. Processes the payload
    /// of the request and logs relevant information. Constructs and returns an appropriate
    /// HTTP response to acknowledge the notification.
    /// </summary>
    /// <param name="req">The HTTP request data containing the notification payload and headers.</param>
    /// <param name="requestId">The unique identifier for the current request, used for logging and tracking.</param>
    /// <returns>An <see cref="HttpResponseData"/> with a 200 OK status code and information acknowledging receipt of the notification.</returns>
    private async Task<HttpResponseData> HandleTestNotification(HttpRequestData req, string requestId)
    {
        try
        {
            // Log request headers
            _logger.LogInformation("[{RequestId}] 📨 Received webhook notification", requestId);

            foreach (var header in req.Headers.Where(h => h.Key.StartsWith("Content-") ||
                                                          h.Key.Equals("User-Agent",
                                                              StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogDebug("[{RequestId}] Header {Key}: {Value}", requestId, header.Key,
                    string.Join(", ", header.Value));
            }

            // Read notification payload
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            if (string.IsNullOrEmpty(requestBody))
            {
                _logger.LogWarning("[{RequestId}] Empty notification body", requestId);
            }
            else
            {
                _logger.LogInformation("[{RequestId}] Notification payload ({Length} chars): {Payload}",
                    requestId, requestBody.Length, requestBody);

                // Try to parse as JSON for better logging
                try
                {
                    using var jsonDoc = JsonDocument.Parse(requestBody);
                    var prettyJson =
                        JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions { WriteIndented = true });
                    _logger.LogInformation("[{RequestId}] 📋 Formatted notification:\n{Json}", requestId, prettyJson);
                }
                catch (JsonException)
                {
                    _logger.LogInformation("[{RequestId}] 📝 Raw notification: {Body}", requestId, requestBody);
                }
            }

            // Microsoft Graph expects 200 OK response
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");

            var responsePayload = JsonSerializer.Serialize(new
            {
                message = "Test notification received successfully",
                requestId = requestId,
                timestamp = DateTimeOffset.UtcNow,
                bodyLength = requestBody.Length 
            });

            await response.WriteStringAsync(responsePayload);

            _logger.LogInformation("[{RequestId}] ✅ Test notification processed successfully", requestId);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{RequestId}] Error processing test notification", requestId);

            // Still return 200 OK to prevent Microsoft Graph from retrying
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync(JsonSerializer.Serialize(new
            {
                message = "Test notification acknowledged with errors",
                requestId = requestId,
                error = ex.Message
            }));
            return response;
        }
    }
}