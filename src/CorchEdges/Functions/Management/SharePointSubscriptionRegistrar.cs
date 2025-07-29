using System.Net;
using System.Text.Json;
using System.Web;
using CorchEdges.Models.Requests;
using CorchEdges.Services;
using CorchEdges.Utilities;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Models;
using Microsoft.OpenApi.Models;

namespace CorchEdges.Functions.Management;

/// <summary>
/// Provides functionality for setting up and managing SharePoint webhooks.
/// </summary>
/// <remarks>
/// This class includes Azure Functions to handle webhook setup,
/// retrieve webhook status, renew subscriptions, delete specific webhooks,
/// and clean up test webhooks.
/// </remarks>
public class SharePointSubscriptionRegistrar(
    WebhookRegistrar webhookRegistrar,
    ILogger<SharePointSubscriptionRegistrar> logger,
    IConfiguration configuration)
{
    /// <summary>
    /// Represents an instance of <see cref="WebhookRegistrar"/> used to interact with and manage
    /// webhook subscriptions in a SharePoint integration context. This variable is utilized
    /// for operations such as checking the existence of webhooks, registering new webhooks,
    /// and managing active or expiring webhook subscriptions.
    /// </summary>
    private readonly WebhookRegistrar _webhookRegistrar =
        webhookRegistrar ?? throw new ArgumentNullException(nameof(webhookRegistrar));

    /// <summary>
    /// Represents the logger instance used for logging information, warnings, errors, and debug messages
    /// within the SharePointSubscriptionRegistrar class. It provides structured logging functionality
    /// to aid in diagnosing issues, tracking application workflows, or providing runtime diagnostics.
    /// </summary>
    private readonly ILogger<SharePointSubscriptionRegistrar>
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Represents the configuration settings for the application, allowing access to configuration values.
    /// </summary>
    /// <remarks>
    /// This variable provides access to application-level configuration settings.
    /// Typical usage includes retrieving settings from a configuration source, such as JSON files,
    /// environment variables, or other configuration providers. The values can be used to configure
    /// application functionality, such as SharePoint webhook setup, callback URLs, or other operational settings.
    /// </remarks>
    private readonly IConfiguration _configuration =
        configuration ?? throw new ArgumentNullException(nameof(configuration));

    /// <summary>
    /// Handles the HTTP request to set up a SharePoint webhook, registering a webhook for a specified list
    /// in a SharePoint site and returning the details of the newly created subscription.
    /// The method processes the request body to retrieve configuration parameters for the webhook setup,
    /// validates the configuration, checks if a webhook is already registered, and registers a new webhook
    /// if necessary. It returns an HTTP response indicating the operation status.
    /// </summary>
    /// <param name="req">
    /// The HTTP request containing the setup details, expected to be a POST request with a JSON body
    /// containing a <see cref="WebhookConfiguration"/> object with the following required properties:
    /// <list type="bullet">
    /// <item><description><c>siteId</c> - The Optional SharePoint site ID (GUID format)</description></item>
    /// <item><description><c>listId</c> - The Optional SharePoint list ID (GUID format)</description></item>
    /// <item><description><c>callbackUrl</c> - The webhook callback URL (absolute URL)</description></item>
    /// <item><description><c>functionAppName</c> - Optional function app name for automatic URL generation</description></item>
    /// </list>
    /// Example request body:
    /// <code>
    /// {
    ///   "siteId": "12345678-1234-1234-1234-123456789012",
    ///   "listId": "87654321-4321-4321-4321-210987654321", 
    ///   "callbackUrl": "https://your-app.azurewebsites.net/api/webhook/callback",
    ///   "functionAppName": "my-function-app"
    /// }
    /// </code>
    /// </param>
    /// <returns>
    /// An asynchronous task that resolves to an HTTP response. This response contains success or error details
    /// based on the outcome of the webhook setup operation. On success, it includes a <see cref="WebhookResponse"/>
    /// object with subscription details; on failure, it includes error details with appropriate HTTP status codes:
    /// <list type="bullet">
    /// <item><description>200 OK - Webhook registered successfully or already exists</description></item>
    /// <item><description>400 Bad Request - Invalid request body, validation errors, or malformed parameters</description></item>
    /// <item><description>408 Request Timeout - Operation was cancelled or timed out</description></item>
    /// <item><description>500 Internal Server Error - Unexpected server errors</description></item>
    /// <item><description>502 Bad Gateway - Microsoft Graph API or network connectivity issues</description></item>
    /// </list>
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when invalid arguments are provided in the request</exception>
    /// <exception cref="InvalidOperationException">Thrown when the webhook registration operation fails</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled or times out</exception>
    /// <exception cref="Microsoft.Graph.Models.ODataErrors.ODataError">Thrown when Microsoft Graph API returns an error</exception>
    /// <exception cref="HttpRequestException">Thrown when network connectivity issues occur</exception>
    /// <example>
    /// Example successful response:
    /// <code>
    /// {
    ///   "message": "Webhook registered successfully",
    ///   "subscriptionId": "abcd1234-5678-9012-3456-789012345678",
    ///   "expirationDateTime": "2024-01-15T12:00:00Z",
    ///   "callbackUrl": "https://your-app.azurewebsites.net/api/webhook/callback",
    ///   "resource": "sites/12345678-1234-1234-1234-123456789012/lists/87654321-4321-4321-4321-210987654321",
    ///   "changeType": "updated",
    ///   "clientState": "your-client-state"
    /// }
    /// </code>
    /// 
    /// Example error response:
    /// <code>
    /// {
    ///   "error": "SiteId is required",
    ///   "timestamp": "2024-01-01T12:00:00Z",
    ///   "requestId": "req-123456"
    /// }
    /// </code>
    /// </example>
    [Function("CreateSharePointSubscription")]
    [OpenApiOperation(
        operationId: "CreateSharePointSubscription", 
        tags: new[] { "SharePoint Subscriptions" },
        Summary = "Create SharePoint webhook subscription",
        Description = "Creates a new webhook subscription to monitor changes in a SharePoint list")]
    [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
    [OpenApiRequestBody(
        contentType: "application/json", 
        bodyType: typeof(WebhookConfiguration),
        Required = true,
        Description = "Webhook configuration containing site ID, list ID, callback URL, and function app name")]
    [OpenApiResponseWithBody(
        statusCode: HttpStatusCode.Created, 
        contentType: "application/json", 
        bodyType: typeof(object), 
        Description = "Subscription created successfully")]
    [OpenApiResponseWithBody(
        statusCode: HttpStatusCode.BadRequest, 
        contentType: "application/json", 
        bodyType: typeof(object), 
        Description = "Invalid configuration or validation error")]
    [OpenApiResponseWithBody(
        statusCode: HttpStatusCode.InternalServerError, 
        contentType: "application/json", 
        bodyType: typeof(object), 
        Description = "Internal server error")]
    public async Task<HttpResponseData> CreateSharePointSubscriptionAsync(
        [HttpTrigger(AuthorizationLevel.Admin, "post", Route = "sharepoint/subscriptions")]
        HttpRequestData req)
    {
        _logger.LogInformation("SharePointSubscriptions function triggered");

        try
        {
            // Parse request body for optional parameters
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var requestData = ParseRequestBody(requestBody);
            
            // Get configuration values
            var config = GetWebhookConfiguration(requestData);
            
            if (!ValidateConfiguration(config, out var validationError))
            {
                _logger.LogError("Configuration validation failed: {Error}", validationError);
                return await CreateErrorResponseAsync(req, HttpStatusCode.BadRequest, validationError);
            }

            // Check if a webhook already exists
            if (await _webhookRegistrar.IsListMonitoredByWebhookAsync(config.SiteId!, config.ListId!))
            {
                _logger.LogInformation("Webhook already registered for site {SiteId}, list {ListId}",
                    config.SiteId, config.ListId);

                return await CreateSuccessResponseAsync(req, new
                {
                    Message = "Webhook already registered and active",
                    config.SiteId,
                    config.ListId,
                    config.CallbackUrl
                });
            }

            // Register new webhook
            var subscription = await _webhookRegistrar.RegisterWebhookAsync(
                config.SiteId!,
                config.ListId!,
                config.CallbackUrl!);

            _logger.LogInformation("Webhook registered successfully. Subscription ID: {SubscriptionId}",
                subscription.Id);

            var response = new WebhookResponse(
                "Webhook registered successfully",
                subscription.Id ?? string.Empty,
                subscription.ExpirationDateTime,
                config.CallbackUrl!,
                subscription.Resource ?? string.Empty,
                subscription.ChangeType ?? string.Empty,
                subscription.ClientState ?? string.Empty);

            return await CreateSuccessResponseAsync(req, response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid argument provided: {Message}", ex.Message);
            return await CreateErrorResponseAsync(req, HttpStatusCode.BadRequest, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Operation failed: {Message}", ex.Message);
            return await CreateErrorResponseAsync(req, HttpStatusCode.BadRequest, ex.Message);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Webhook registration was cancelled");
            return await CreateErrorResponseAsync(req, HttpStatusCode.RequestTimeout, "Request was cancelled");
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
        {
            _logger.LogError(ex, "Microsoft Graph API error during webhook registration: {Error}",
                ex.Error?.Message ?? ex.Message);
            return await CreateErrorResponseAsync(req, HttpStatusCode.BadGateway,
                $"Microsoft Graph API error: {ex.Error?.Message ?? ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error during webhook registration: {Message}", ex.Message);
            return await CreateErrorResponseAsync(req, HttpStatusCode.BadGateway,
                "Network connectivity issue occurred");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during webhook registration");
            return await CreateErrorResponseAsync(req, HttpStatusCode.InternalServerError, "Internal server error");
        }
    }

    /// Retrieves the current status of SharePoint webhooks, including active subscriptions
    /// and those nearing expiration.
    /// <param name="req">The HTTP request data triggering this function.</param>
    /// <returns>A response containing the status of active webhooks, including
    /// total active subscriptions, those expiring soon, and metadata for each subscription.</returns>
    [Function("GetSharePointSubscriptions")]
    [OpenApiOperation(
        operationId: "GetSharePointSubscriptions", 
        tags: new[] { "SharePoint Subscriptions" },
        Summary = "Get all SharePoint subscriptions",
        Description = "Retrieves all active SharePoint webhook subscriptions for the configured site and list")]
    [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
    [OpenApiResponseWithBody(
        statusCode: HttpStatusCode.OK, 
        contentType: "application/json", 
        bodyType: typeof(object[]), 
        Description = "List of active subscriptions")]
    [OpenApiResponseWithBody(
        statusCode: HttpStatusCode.InternalServerError, 
        contentType: "application/json", 
        bodyType: typeof(object), 
        Description = "Internal server error")]
    public async Task<HttpResponseData> GetSharePointSubscriptionsAsync(
        [HttpTrigger(AuthorizationLevel.Admin, "get", Route = "sharepoint/subscriptions")]
        HttpRequestData req)
    {
        _logger.LogInformation("GetSharePointSubscriptionsAsync function triggered");

        try
        {
            var activeSubscriptions = await _webhookRegistrar.GetActiveSubscriptionsAsync();
            var expiringSubscriptions = await _webhookRegistrar.GetExpiringSubscriptionsAsync();

            var subscriptions = activeSubscriptions as Subscription[] ?? activeSubscriptions.ToArray();
            var expiring = expiringSubscriptions as Subscription[] ?? expiringSubscriptions.ToArray();

            var response = new
            {
                ActiveSubscriptions = subscriptions.Select(s => new
                {
                    s.Id,
                    s.Resource,
                    s.ChangeType,
                    s.NotificationUrl,
                    s.ExpirationDateTime,
                    s.ClientState,
                    IsExpiringSoon = s.ExpirationDateTime <= DateTimeOffset.UtcNow.AddHours(24)
                }),
                TotalActive = subscriptions.Length,
                ExpiringSoon = expiring.Length,
                CheckedAt = DateTimeOffset.UtcNow
            };

            return await CreateSuccessResponseAsync(req, response);
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
        {
            _logger.LogError(ex, "Microsoft Graph API error retrieving webhook status: {Error}",
                ex.Error?.Message ?? ex.Message);
            return await CreateErrorResponseAsync(req, HttpStatusCode.BadGateway,
                $"Microsoft Graph API error: {ex.Error?.Message ?? ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error retrieving webhook status: {Message}", ex.Message);
            return await CreateErrorResponseAsync(req, HttpStatusCode.BadGateway,
                "Network connectivity issue occurred");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving webhook status");
            return await CreateErrorResponseAsync(req, HttpStatusCode.InternalServerError, "Internal server error");
        }
    }

    /// Handles the renewal of expiring webhooks by identifying expiring subscriptions
    /// and attempting to renew them individually.
    /// <param name="req">The HTTP request data that triggers the function.</param>
    /// <returns>A <see cref="HttpResponseData"/> object containing the results of the renewal process. The response includes
    /// details of the processed subscriptions, the number of successful and failed renewals, and the processing timestamp.</returns>
    [Function("RenewSharePointSubscriptions")]
    [OpenApiOperation(
        operationId: "RenewSharePointSubscriptions", 
        tags: new[] { "SharePoint Subscriptions" },
        Summary = "Renew SharePoint subscriptions",
        Description = "Renews all existing SharePoint webhook subscriptions to extend their expiration date")]
    [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
    [OpenApiResponseWithBody(
        statusCode: HttpStatusCode.OK, 
        contentType: "application/json", 
        bodyType: typeof(object), 
        Description = "Subscriptions renewed successfully")]
    [OpenApiResponseWithBody(
        statusCode: HttpStatusCode.InternalServerError, 
        contentType: "application/json", 
        bodyType: typeof(object), 
        Description = "Internal server error")]
    public async Task<HttpResponseData> RenewSharePointSubscriptionsAsync(
        [HttpTrigger(AuthorizationLevel.Admin, "patch", Route = "sharepoint/subscriptions/renew")]
        HttpRequestData req)
    {
        _logger.LogInformation("RenewSharePointSubscriptionsAsync function triggered");

        try
        {
            var expiringSubscriptions = await _webhookRegistrar.GetExpiringSubscriptionsAsync();
            var renewalResults = new List<object>();

            foreach (var subscription in expiringSubscriptions)
            {
                if (string.IsNullOrEmpty(subscription.Id))
                {
                    _logger.LogWarning("Skipping subscription with null/empty ID");
                    continue;
                }

                try
                {
                    var success = await _webhookRegistrar.RenewSubscriptionAsync(subscription.Id);
                    renewalResults.Add(new
                    {
                        SubscriptionId = subscription.Id,
                        subscription.Resource,
                        Success = success,
                        PreviousExpiration = subscription.ExpirationDateTime,
                        NewExpiration = success ? DateTimeOffset.UtcNow.AddDays(3) : (DateTimeOffset?)null,
                        Error = (string?)null
                    });

                    _logger.LogInformation("Subscription {SubscriptionId} renewal {Status}",
                        subscription.Id, success ? "succeeded" : "failed");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to renew subscription {SubscriptionId}: {Error}",
                        subscription.Id, ex.Message);

                    renewalResults.Add(new
                    {
                        SubscriptionId = subscription.Id,
                        subscription.Resource,
                        Success = false,
                        PreviousExpiration = subscription.ExpirationDateTime,
                        NewExpiration = (DateTimeOffset?)null,
                        Error = ex.Message
                    });
                }
            }

            var successfulRenewals = renewalResults.Count(r => (bool)r.GetType().GetProperty("Success")!.GetValue(r)!);

            var response = new
            {
                Message = $"Processed {renewalResults.Count} subscriptions for renewal",
                Results = renewalResults,
                SuccessfulRenewals = successfulRenewals,
                FailedRenewals = renewalResults.Count - successfulRenewals,
                ProcessedAt = DateTimeOffset.UtcNow
            };

            return await CreateSuccessResponseAsync(req, response);
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
        {
            _logger.LogError(ex, "Microsoft Graph API error renewing webhooks: {Error}",
                ex.Error?.Message ?? ex.Message);
            return await CreateErrorResponseAsync(req, HttpStatusCode.BadGateway,
                $"Microsoft Graph API error: {ex.Error?.Message ?? ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error renewing webhooks: {Message}", ex.Message);
            return await CreateErrorResponseAsync(req, HttpStatusCode.BadGateway,
                "Network connectivity issue occurred");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error renewing webhooks");
            return await CreateErrorResponseAsync(req, HttpStatusCode.InternalServerError, "Internal server error");
        }
    }

    /// <summary>
    /// Deletes an existing webhook subscription using the provided subscription ID.
    /// </summary>
    /// <param name="req">The HTTP request data triggering the function, which includes necessary request headers and context.</param>
    /// <param name="subscriptionId">The ID of the webhook subscription to be deleted.</param>
    /// <returns>An <see cref="HttpResponseData"/> indicating the result of the delete operation,
    /// including success or the error encountered during the process.</returns>
    [Function("DeleteSharePointSubscription")]
    [OpenApiOperation(
        operationId: "DeleteSharePointSubscription", 
        tags: new[] { "SharePoint Subscriptions" },
        Summary = "Delete SharePoint subscription",
        Description = "Deletes a specific SharePoint webhook subscription by ID")]
    [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
    [OpenApiParameter(
        name: "subscriptionId", 
        In = ParameterLocation.Path, 
        Required = true, 
        Type = typeof(string),
        Description = "The unique identifier of the subscription to delete")]
    [OpenApiResponseWithBody(
        statusCode: HttpStatusCode.OK, 
        contentType: "application/json", 
        bodyType: typeof(object), 
        Description = "Subscription deleted successfully")]
    [OpenApiResponseWithBody(
        statusCode: HttpStatusCode.NotFound, 
        contentType: "application/json", 
        bodyType: typeof(object), 
        Description = "Subscription not found")]
    [OpenApiResponseWithBody(
        statusCode: HttpStatusCode.InternalServerError, 
        contentType: "application/json", 
        bodyType: typeof(object), 
        Description = "Internal server error")]
    public async Task<HttpResponseData> DeleteSubscriptionAsync(
        [HttpTrigger(AuthorizationLevel.Admin, "delete", Route = "sharepoint/subscriptions/{subscriptionId}")]
        HttpRequestData req,
        string subscriptionId)
    {
        _logger.LogInformation("DeleteSubscriptionAsync function triggered for subscription {SubscriptionId}", subscriptionId);

        try
        {
            if (string.IsNullOrWhiteSpace(subscriptionId))
            {
                _logger.LogWarning("DeleteSubscriptionAsync called with null/empty subscription ID");
                return await CreateErrorResponseAsync(req, HttpStatusCode.BadRequest, "Subscription ID is required");
            }

            var success = await _webhookRegistrar.DeleteSubscriptionAsync(subscriptionId);

            if (success)
            {
                _logger.LogInformation("Webhook {SubscriptionId} deleted successfully", subscriptionId);
                return await CreateSuccessResponseAsync(req, new
                {
                    Message = "Webhook deleted successfully",
                    SubscriptionId = subscriptionId,
                    DeletedAt = DateTimeOffset.UtcNow
                });
            }
            else
            {
                _logger.LogWarning("Failed to delete webhook {SubscriptionId} - webhook may not exist", subscriptionId);
                return await CreateErrorResponseAsync(req, HttpStatusCode.NotFound,
                    "Webhook not found or already deleted");
            }
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
        {
            _logger.LogError(ex, "Microsoft Graph API error deleting webhook {SubscriptionId}: {Error}",
                subscriptionId, ex.Error?.Message ?? ex.Message);
            return await CreateErrorResponseAsync(req, HttpStatusCode.BadGateway,
                $"Microsoft Graph API error: {ex.Error?.Message ?? ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error deleting webhook {SubscriptionId}: {Message}", subscriptionId,
                ex.Message);
            return await CreateErrorResponseAsync(req, HttpStatusCode.BadGateway,
                "Network connectivity issue occurred");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting webhook {SubscriptionId}", subscriptionId);
            return await CreateErrorResponseAsync(req, HttpStatusCode.InternalServerError, "Internal server error");
        }
    }

    /// Cleans up test webhooks by identifying and deleting subscriptions that match a specific pattern or filter.
    /// Subscriptions with a "ClientState" that starts with "test-" will be targeted for cleanup.
    /// An optional filter can be applied to further refine the selection of test subscriptions.
    /// <param name="req">The HTTP request data, which may include query parameters for filtering subscriptions, such as "testId".</param>
    /// <returns>An HTTP response containing the cleanup results, which includes the number of processed, successfully deleted, and failed test subscriptions.</returns>
    [Function("CleanupTestSubscriptions")]
    [OpenApiOperation(
        operationId: "CleanupTestSubscriptions", 
        tags: new[] { "SharePoint Subscriptions" },
        Summary = "Cleanup test subscriptions",
        Description = "Removes all test or development SharePoint webhook subscriptions")]
    [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
    [OpenApiResponseWithBody(
        statusCode: HttpStatusCode.OK, 
        contentType: "application/json", 
        bodyType: typeof(object), 
        Description = "Test subscriptions cleaned up successfully")]
    [OpenApiResponseWithBody(
        statusCode: HttpStatusCode.InternalServerError, 
        contentType: "application/json", 
        bodyType: typeof(object), 
        Description = "Internal server error")]
    public async Task<HttpResponseData> CleanupTestSubscriptionsAsync(
        [HttpTrigger(AuthorizationLevel.Admin, "delete", Route = "sharepoint/subscriptions/test")]
        HttpRequestData req)
    {
        _logger.LogInformation("CleanupTestSubscriptionsAsync function triggered");

        try
        {
            var queryParams = HttpUtility.ParseQueryString(req.Url.Query);
            var testIdFilter = queryParams["testId"]; // Optional filter

            var activeSubscriptions = await _webhookRegistrar.GetActiveSubscriptionsAsync();

            // Find test subscriptions
            var testSubscriptions = activeSubscriptions.Where(s =>
                !string.IsNullOrEmpty(s.ClientState) &&
                s.ClientState.StartsWith("test-") &&
                (testIdFilter == null || s.ClientState.Contains(testIdFilter))).ToList();

            var deletionResults = new List<object>();

            foreach (var subscription in testSubscriptions)
            {
                if (string.IsNullOrEmpty(subscription.Id)) continue;

                try
                {
                    var success = await _webhookRegistrar.DeleteSubscriptionAsync(subscription.Id);
                    deletionResults.Add(new
                    {
                        SubscriptionId = subscription.Id,
                        subscription.ClientState,
                        Success = success,
                        Error = (string?)null
                    });
                }
                catch (Exception ex)
                {
                    deletionResults.Add(new
                    {
                        SubscriptionId = subscription.Id,
                        subscription.ClientState,
                        Success = false,
                        Error = ex.Message
                    });
                }
            }

            var successfulDeletes = deletionResults.Count(r => (bool)r.GetType().GetProperty("Success")!.GetValue(r)!);

            var response = new
            {
                Message = $"Processed {testSubscriptions.Count} test subscriptions for cleanup",
                TestIdFilter = testIdFilter,
                Results = deletionResults,
                SuccessfulDeletes = successfulDeletes,
                FailedDeletes = deletionResults.Count - successfulDeletes,
                ProcessedAt = DateTimeOffset.UtcNow
            };

            return await CreateSuccessResponseAsync(req, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up test webhooks");
            return await CreateErrorResponseAsync(req, HttpStatusCode.InternalServerError, "Internal server error");
        }
    }

    /// Creates an HTTP success response with the given data serialized as JSON.
    /// <param name="req">The incoming HTTP request, used to create the response.</param>
    /// <param name="data">The object data to be serialized into the response body.</param>
    /// <return>
    /// An HTTP success response with a JSON representation of the provided data.
    /// If an error occurs during response creation or serialization, returns an HTTP error response.
    /// </return>
    private async Task<HttpResponseData> CreateSuccessResponseAsync(HttpRequestData req, object data)
    {
        try
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create success response");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json");
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(
                new { Error = "Failed to serialize response" }, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));
            return errorResponse;
        }
    }

    /// Creates an error response with the specified status code and error message.
    /// <param name="req">
    /// The HTTP request data from which the response is created.
    /// </param>
    /// <param name="statusCode">
    /// The HTTP status code to set for the response.
    /// </param>
    /// <param name="message">
    /// The error message to include in the response body.
    /// </param>
    /// <return>
    /// A Task representing the asynchronous operation that returns an HttpResponseData
    /// containing the error response.
    /// </return>
    private async Task<HttpResponseData> CreateErrorResponseAsync(HttpRequestData req, HttpStatusCode statusCode,
        string message)
    {
        try
        {
            var response = req.CreateResponse(statusCode);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(new { Error = message }, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create error response for message: {Message}", message);
            var fallbackResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            fallbackResponse.Headers.Add("Content-Type", "text/plain");
            await fallbackResponse.WriteStringAsync("Internal server error");
            return fallbackResponse;
        }
    }

    /// Retrieves the webhook configuration based on the given request data.
    /// Combines values from the request data, application configuration, and environment variables
    /// to construct the necessary configuration for the webhook setup.
    /// <param name="requestData">
    /// A dictionary containing key-value pairs with optional parameters such as siteId, listId, functionKey,
    /// and callbackUrl that override the default configuration values.
    /// </param>
    /// <returns>
    /// An instance of WebhookConfiguration containing the site ID, list ID, callback URL, and optional
    /// function app name that are required for webhook registration.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when mandatory parameters such as siteId, listId, or callbackUrl are missing or empty.
    /// </exception>
    /// <exception cref="Exception">
    /// Thrown when there is an unhandled error during the retrieval of the webhook configuration.
    /// </exception>
    private WebhookConfiguration GetWebhookConfiguration(Dictionary<string, object> requestData)
    {
        try
        {
            // Priority: Request body > Configuration > Environment variables
            var siteId = requestData.GetConfigValue("siteId") ??
                         _configuration["SharePoint:SiteId"];

            var listId = requestData.GetConfigValue("listId") ??
                         _configuration["SharePoint:ListId"];

            var functionAppName = _configuration["WEBSITE_SITE_NAME"];

            var functionKey = requestData.GetConfigValue("functionKey") ??
                              _configuration["WebhookFunctionKey"];

            // Make function key required
            if (string.IsNullOrEmpty(functionKey))
            {
                throw new InvalidOperationException(
                    "Function key is required for webhook security. " +
                    "Configure WebhookFunctionKey in settings or provide functionKey in request body.");
            }

            var callbackUrl = requestData.GetConfigValue("callbackUrl");

            if (string.IsNullOrEmpty(callbackUrl))
            {
                if (string.IsNullOrEmpty(functionAppName))
                {
                    throw new InvalidOperationException(
                        "Cannot generate callback URL: WEBSITE_SITE_NAME is not available and no explicit callbackUrl provided");
                }

                callbackUrl = $"https://{functionAppName}.azurewebsites.net/sharepoint/webhook?code={functionKey}";
            }

            if (string.IsNullOrEmpty(siteId))
                throw new ArgumentException("SiteId cannot be null or empty", nameof(siteId));
            if (string.IsNullOrEmpty(listId))
                throw new ArgumentException("ListId cannot be null or empty", nameof(listId));
            if (string.IsNullOrEmpty(callbackUrl))
                throw new ArgumentException("CallbackUrl cannot be null or empty", nameof(callbackUrl));

            return new WebhookConfiguration(siteId, listId, callbackUrl, functionAppName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get webhook configuration");
            throw;
        }
    }

    /// <summary>
    /// Parses the request body into a dictionary of key-value pairs.
    /// </summary>
    /// <param name="requestBody">The JSON string representing the request body to be parsed.</param>
    /// <returns>A dictionary containing the parsed key-value pairs from the request body.
    /// Returns an empty dictionary if the request body is null, empty, or invalid.</returns>
    private Dictionary<string, object> ParseRequestBody(string requestBody)
    {
        if (string.IsNullOrWhiteSpace(requestBody))
        {
            return new Dictionary<string, object>();
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, object>>(requestBody);
            return parsed ?? new Dictionary<string, object>();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse request body as JSON, using empty configuration");
            return new Dictionary<string, object>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error parsing request body");
            return new Dictionary<string, object>();
        }
    }

    /// Validates the provided webhook configuration values to ensure they meet the required criteria.
    /// <param name="config">The configuration object containing the webhook setup details.</param>
    /// <param name="error">
    /// An output parameter that will contain an error message if validation fails. Returns an empty string if validation is successful.
    /// </param>
    /// <returns>
    /// A boolean value indicating whether the configuration is valid. Returns true if valid, and false otherwise.
    /// </returns>
    private static bool ValidateConfiguration(WebhookConfiguration config, out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(config.SiteId))
        {
            error = "SharePoint Site ID is required. Configure SharePoint:SiteId or provide in request body.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(config.ListId))
        {
            error = "SharePoint List ID is required. Configure SharePoint:ListId or provide in request body.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(config.CallbackUrl))
        {
            error =
                "Callback URL could not be determined. Ensure WEBSITE_SITE_NAME is available or provide callbackUrl in request body.";
            return false;
        }

        if (!Uri.TryCreate(config.CallbackUrl, UriKind.Absolute, out var uri) ||
            !uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            error = "Callback URL must be a valid HTTPS URL.";
            return false;
        }

        // Validate that the URL includes authentication
        if (!HasAuthenticationParameter(config.CallbackUrl))
        {
            error = "Callback URL must include authentication (function key). Use ?code=<function-key> parameter.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Determines if the given URL contains an authentication parameter, specifically a "code" parameter.
    /// </summary>
    /// <param name="url">The URL to check for the presence of an authentication parameter.</param>
    /// <returns>True if the URL contains a "code" parameter; otherwise, false.</returns>
    private static bool HasAuthenticationParameter(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        var query = HttpUtility.ParseQueryString(uri.Query);
        return !string.IsNullOrEmpty(query["code"]);
    }
}