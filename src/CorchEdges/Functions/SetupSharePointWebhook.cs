using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using CorchEdges.Services;
using CorchEdges.Utilities;
using Microsoft.Graph.Models;

namespace CorchEdges.Functions;

public class SharePointSetupWebhook(
    WebhookRegistration webhookRegistration,
    ILogger<SharePointSetupWebhook> logger,
    IConfiguration configuration)
{
    private readonly WebhookRegistration _webhookRegistration = webhookRegistration ?? throw new ArgumentNullException(nameof(webhookRegistration));
    private readonly ILogger<SharePointSetupWebhook> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IConfiguration _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

    [Function("SharePointSetupWebhook")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Admin, "post", Route = "setup")] HttpRequestData req)
    {
        _logger.LogInformation("SharePointSetupWebhook function triggered");

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
            if (await _webhookRegistration.IsListMonitoredByWebhookAsync(config.SiteId, config.ListId))
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
            var subscription = await _webhookRegistration.RegisterWebhookAsync(
                config.SiteId, 
                config.ListId, 
                config.CallbackUrl);
            
            _logger.LogInformation("Webhook registered successfully. Subscription ID: {SubscriptionId}", 
                subscription.Id);
                
            return await CreateSuccessResponseAsync(req, new 
            { 
                Message = "Webhook registered successfully",
                SubscriptionId = subscription.Id,
                subscription.ExpirationDateTime,
                config.CallbackUrl,
                subscription.Resource,
                subscription.ChangeType,
                subscription.ClientState
            });
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
            _logger.LogError(ex, "Microsoft Graph API error during webhook registration: {Error}", ex.Error?.Message ?? ex.Message);
            return await CreateErrorResponseAsync(req, HttpStatusCode.BadGateway, $"Microsoft Graph API error: {ex.Error?.Message ?? ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error during webhook registration: {Message}", ex.Message);
            return await CreateErrorResponseAsync(req, HttpStatusCode.BadGateway, "Network connectivity issue occurred");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during webhook registration");
            return await CreateErrorResponseAsync(req, HttpStatusCode.InternalServerError, "Internal server error");
        }
    }

    [Function("GetWebhookStatus")]
    public async Task<HttpResponseData> GetStatus(
        [HttpTrigger(AuthorizationLevel.Admin, "get", Route = "setup/status")] HttpRequestData req)
    {
        _logger.LogInformation("GetWebhookStatus function triggered");

        try
        {
            var activeSubscriptions = await _webhookRegistration.GetActiveSubscriptionsAsync();
            var expiringSubscriptions = await _webhookRegistration.GetExpiringSubscriptionsAsync();

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
            _logger.LogError(ex, "Microsoft Graph API error retrieving webhook status: {Error}", ex.Error?.Message ?? ex.Message);
            return await CreateErrorResponseAsync(req, HttpStatusCode.BadGateway, $"Microsoft Graph API error: {ex.Error?.Message ?? ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error retrieving webhook status: {Message}", ex.Message);
            return await CreateErrorResponseAsync(req, HttpStatusCode.BadGateway, "Network connectivity issue occurred");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving webhook status");
            return await CreateErrorResponseAsync(req, HttpStatusCode.InternalServerError, "Internal server error");
        }
    }

    [Function("RenewWebhooks")]
    public async Task<HttpResponseData> RenewWebhooks(
        [HttpTrigger(AuthorizationLevel.Admin, "post", Route = "setup/renew")] HttpRequestData req)
    {
        _logger.LogInformation("RenewWebhooks function triggered");

        try
        {
            var expiringSubscriptions = await _webhookRegistration.GetExpiringSubscriptionsAsync();
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
                    var success = await _webhookRegistration.RenewSubscriptionAsync(subscription.Id);
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
            _logger.LogError(ex, "Microsoft Graph API error renewing webhooks: {Error}", ex.Error?.Message ?? ex.Message);
            return await CreateErrorResponseAsync(req, HttpStatusCode.BadGateway, $"Microsoft Graph API error: {ex.Error?.Message ?? ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error renewing webhooks: {Message}", ex.Message);
            return await CreateErrorResponseAsync(req, HttpStatusCode.BadGateway, "Network connectivity issue occurred");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error renewing webhooks");
            return await CreateErrorResponseAsync(req, HttpStatusCode.InternalServerError, "Internal server error");
        }
    }

    [Function("DeleteWebhook")]
    public async Task<HttpResponseData> DeleteWebhook(
        [HttpTrigger(AuthorizationLevel.Admin, "delete", Route = "setup/{subscriptionId}")] HttpRequestData req,
        string subscriptionId)
    {
        _logger.LogInformation("DeleteWebhook function triggered for subscription {SubscriptionId}", subscriptionId);

        try
        {
            if (string.IsNullOrWhiteSpace(subscriptionId))
            {
                _logger.LogWarning("DeleteWebhook called with null/empty subscription ID");
                return await CreateErrorResponseAsync(req, HttpStatusCode.BadRequest, "Subscription ID is required");
            }

            var success = await _webhookRegistration.DeleteSubscriptionAsync(subscriptionId);
            
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
                return await CreateErrorResponseAsync(req, HttpStatusCode.NotFound, "Webhook not found or already deleted");
            }
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
        {
            _logger.LogError(ex, "Microsoft Graph API error deleting webhook {SubscriptionId}: {Error}", 
                subscriptionId, ex.Error?.Message ?? ex.Message);
            return await CreateErrorResponseAsync(req, HttpStatusCode.BadGateway, $"Microsoft Graph API error: {ex.Error?.Message ?? ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error deleting webhook {SubscriptionId}: {Message}", subscriptionId, ex.Message);
            return await CreateErrorResponseAsync(req, HttpStatusCode.BadGateway, "Network connectivity issue occurred");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting webhook {SubscriptionId}", subscriptionId);
            return await CreateErrorResponseAsync(req, HttpStatusCode.InternalServerError, "Internal server error");
        }
    }

    [Function("CleanupTestWebhooks")]
    public async Task<HttpResponseData> CleanupTestWebhooks(
        [HttpTrigger(AuthorizationLevel.Admin, "delete", Route = "setup/cleanup/test")]
        HttpRequestData req)
    {
        _logger.LogInformation("CleanupTestWebhooks function triggered");

        try
        {
            var queryParams = HttpUtility.ParseQueryString(req.Url.Query);
            var testIdFilter = queryParams["testId"]; // Optional filter

            var activeSubscriptions = await _webhookRegistration.GetActiveSubscriptionsAsync();

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
                    var success = await _webhookRegistration.DeleteSubscriptionAsync(subscription.Id);
                    deletionResults.Add(new
                    {
                        SubscriptionId = subscription.Id,
                        ClientState = subscription.ClientState,
                        Success = success,
                        Error = (string?)null
                    });
                }
                catch (Exception ex)
                {
                    deletionResults.Add(new
                    {
                        SubscriptionId = subscription.Id,
                        ClientState = subscription.ClientState,
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
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { Error = "Failed to serialize response" }, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            }));
            return errorResponse;
        }
    }

    private async Task<HttpResponseData> CreateErrorResponseAsync(HttpRequestData req, HttpStatusCode statusCode, string message)
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

    private WebhookConfiguration GetWebhookConfiguration(Dictionary<string, object> requestData)
    {
        try
        {
            // Priority: Request body > Configuration > Environment variables
            var siteId = requestData.GetConfigValue("siteId") ?? 
                        _configuration["SharePoint:SiteId"] ?? 
                        Environment.GetEnvironmentVariable("SharePoint:SiteId");

            var listId = requestData.GetConfigValue("listId") ?? 
                        _configuration["SharePoint:ListId"] ?? 
                        Environment.GetEnvironmentVariable("SharePoint:ListId");

            var functionAppName = _configuration["WEBSITE_SITE_NAME"] ?? 
                                 Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME");

            var functionKey = requestData.GetConfigValue("functionKey") ?? 
                             _configuration["WebhookFunctionKey"] ?? 
                             Environment.GetEnvironmentVariable("WebhookFunctionKey");

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
                    throw new InvalidOperationException("Cannot generate callback URL: WEBSITE_SITE_NAME is not available and no explicit callbackUrl provided");
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
            error = "Callback URL could not be determined. Ensure WEBSITE_SITE_NAME is available or provide callbackUrl in request body.";
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

    private static bool HasAuthenticationParameter(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        return !string.IsNullOrEmpty(query["code"]);
    }

    private class WebhookConfiguration(string siteId, string listId, string callbackUrl, string? functionAppName)
    {
        public string SiteId { get; init; } = siteId;
        public string ListId { get; init; } = listId;
        public string CallbackUrl { get; init; } = callbackUrl;
        
        public string? FunctionAppName { get; init; } = functionAppName;
    }
}