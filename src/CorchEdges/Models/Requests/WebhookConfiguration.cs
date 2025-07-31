using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;

namespace CorchEdges.Models.Requests;

/// <summary>
/// Represents the configuration details required to set up a SharePoint webhook.
/// This class is used to encapsulate the essential data needed for webhook registration and operation.
/// </summary>
public class WebhookConfiguration(string? siteId, string? listId, string? callbackUrl, string? functionAppName)
{
    /// <summary>
    /// The SharePoint site ID (GUID format). If not provided, will use the configured default site ID.
    /// </summary>
    /// <example>12345678-1234-1234-1234-123456789012</example>
    [OpenApiProperty(Description = "SharePoint site ID in GUID format. Optional if configured in app settings.")]
    public string? SiteId { get; } = siteId;

    /// <summary>
    /// The SharePoint list ID (GUID format). If not provided, will use the configured default list ID.
    /// </summary>
    /// <example>87654321-4321-4321-4321-210987654321</example>
    [OpenApiProperty(Description = "SharePoint list ID in GUID format. Optional if configured in app settings.")]
    public string? ListId { get; } = listId;

    /// <summary>
    /// The webhook callback URL. Must be an HTTPS URL with authentication parameter (?code=function-key).
    /// If not provided, will be auto-generated using the function app name and function key.
    /// </summary>
    /// <example>https://your-app.azurewebsites.net/sharepoint/webhook?code=your-function-key</example>
    [OpenApiProperty(Description = "HTTPS callback URL with authentication. Auto-generated if not provided.")]
    public string? CallbackUrl { get; } = callbackUrl;

    /// <summary>
    /// The Azure Function App name used for auto-generating callback URLs. 
    /// Typically populated automatically from WEBSITE_SITE_NAME environment variable.
    /// </summary>
    /// <example>my-function-app</example>
    [OpenApiProperty(Description = "Function app name for URL generation. Usually auto-detected.")]
    public string? FunctionAppName { get; init; } = functionAppName;

    /// <summary>
    /// The function key used for webhook authentication. Should be provided in request body for security.
    /// </summary>
    /// <example>abc123def456ghi789jkl012mno345pqr678stu901vwx234yz==</example>
    [OpenApiProperty(Description = "Function authorization key for webhook security. Required for webhook setup.")]
    public string? FunctionKey { get; init; }
}