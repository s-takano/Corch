using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;

namespace CorchEdges.Models.Requests;

/// <summary>
/// Configuration settings for SharePoint webhook registration and management.
/// Contains the necessary parameters to establish webhook subscriptions with Microsoft Graph API
/// for monitoring SharePoint list changes.
/// </summary>
/// <param name="SiteId">
/// The SharePoint site ID in GUID format (e.g., "12345678-1234-1234-1234-123456789012"). 
/// This parameter is required and must be provided in the request body.
/// </param>
/// <param name="ListId">
/// The SharePoint list ID in GUID format (e.g., "87654321-4321-4321-4321-210987654321"). 
/// This parameter is required and must be provided in the request body.
/// </param>
/// <param name="WebhookPath">
/// The custom webhook endpoint path that defines the API route for receiving SharePoint notifications
/// (e.g., "sharepoint/webhook", "custom/handler", "webhooks/sharepoint").
/// This parameter is required and must be provided in the request body.
/// The path should not include leading/trailing slashes as they are handled automatically.
/// This path is combined with FunctionAppName and FunctionKey to generate the complete webhook URL on-the-fly.
/// </param>
/// <param name="FunctionAppName">
/// The Azure Function App name used for constructing the callback URL (e.g., "my-function-app"). 
/// This parameter is required and must be provided in the request body.
/// The webhook URL is generated as: https://{FunctionAppName}.azurewebsites.net/api/{WebhookPath}?code={FunctionKey}
/// </param>
/// <param name="FunctionKey">
/// The function key used for webhook authentication (e.g., "abc123def456ghi789jkl012mno345pqr678stu901vwx234yz==").
/// This parameter is required and must be provided in the request body for security.
/// This key authenticates incoming webhook notifications from Microsoft Graph.
/// </param>
/// <remarks>
/// All parameters are mandatory and must be provided in the request body:
/// <list type="bullet">
/// <item><description>siteId - SharePoint site identifier (required)</description></item>
/// <item><description>listId - SharePoint list identifier (required)</description></item>
/// <item><description>functionAppName - Azure Function App name (required)</description></item>
/// <item><description>functionKey - Function access key (required)</description></item>
/// <item><description>webhookPath - Custom webhook endpoint path (required)</description></item>
/// </list>
/// The complete webhook URL is generated dynamically when needed using the format: 
/// https://{functionAppName}.azurewebsites.net/api/{webhookPath}?code={functionKey}
/// </remarks>
/// <example>
/// <code>
/// // Request body example:
/// {
///   "siteId": "12345678-1234-1234-1234-123456789012",
///   "listId": "87654321-4321-4321-4321-210987654321", 
///   "functionAppName": "my-function-app",
///   "functionKey": "abc123def456...",
///   "webhookPath": "sharepoint/webhook"
/// }
/// 
/// // This creates a WebhookConfiguration storing the parameters:
/// var config = new WebhookConfiguration(
///     SiteId: "12345678-1234-1234-1234-123456789012",
///     ListId: "87654321-4321-4321-4321-210987654321", 
///     WebhookPath: "sharepoint/webhook",
///     FunctionAppName: "my-function-app",
///     FunctionKey: "abc123def456..."
/// );
/// 
/// // The webhook URL is generated when needed:
/// // https://my-function-app.azurewebsites.net/api/sharepoint/webhook?code=abc123def456...
/// </code>
/// </example>
public record WebhookConfiguration(
    [property:
        OpenApiProperty(Description = "SharePoint site ID in GUID format. ")]
    string? SiteId,
    [property:
        OpenApiProperty(Description = "SharePoint list ID in GUID format. ")]
    string? ListId,
    [property: OpenApiProperty(Description = "Function app name for URL generation. ")]
    string? FunctionAppName,
    [property: OpenApiProperty(Description = "extra webhook path for URL generation. ")]
    string? WebhookPath,
    [property:
        OpenApiProperty(Description = "Function authorization key for webhook security. Required for webhook setup.")]
    string? FunctionKey)
{
    /// <summary>
    /// Creates a WebhookConfiguration from a dictionary of request parameters.
    /// </summary>
    /// <param name="requestData">Dictionary containing the required webhook parameters.</param>
    /// <returns>A new WebhookConfiguration instance.</returns>
    /// <exception cref="ArgumentException">Thrown when required parameters are missing or invalid.</exception>
    public static WebhookConfiguration Create(Dictionary<string, object> requestData)
    {
        // Factory logic here
        var siteId = GetRequiredValue(requestData, "siteId");
        var listId = GetRequiredValue(requestData, "listId");
        var webhookPath = GetRequiredValue(requestData, "webhookPath");
        var functionAppName = GetRequiredValue(requestData, "functionAppName");
        var functionKey = GetRequiredValue(requestData, "functionKey");

        return new WebhookConfiguration(siteId, listId, webhookPath, functionAppName, functionKey);
    }

    private static string GetRequiredValue(Dictionary<string, object> data, string key)
    {
        if (!data.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value?.ToString()))
            throw new ArgumentException($"Required parameter '{key}' is missing or empty.");

        return value.ToString()!;
    }
}