using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;

namespace CorchEdges.Models.Requests;

/// <summary>
/// Configuration settings for SharePoint webhook registration and management.
/// Contains the necessary parameters to establish webhook subscriptions with Microsoft Graph API
/// for monitoring SharePoint list changes.
/// </summary>
/// <param name="SiteId">
/// The SharePoint site ID in GUID format (e.g., "12345678-1234-1234-1234-123456789012"). 
/// If not provided, the system will use the configured default site ID from app settings.
/// </param>
/// <param name="ListId">
/// The SharePoint list ID in GUID format (e.g., "87654321-4321-4321-4321-210987654321"). 
/// If not provided, the system will use the configured default list ID from app settings.
/// </param>
/// <param name="CallbackUrl">
/// The webhook callback URL that will receive notifications from Microsoft Graph. 
/// Must be an HTTPS URL with authentication parameter (e.g., "https://your-app.azurewebsites.net/sharepoint/webhook?code=your-function-key").
/// If not provided, the URL will be auto-generated using the function app name and function key.
/// </param>
/// <param name="FunctionAppName">
/// The Azure Function App name used for auto-generating callback URLs (e.g., "my-function-app"). 
/// This is typically populated automatically from the WEBSITE_SITE_NAME environment variable 
/// and is used when CallbackUrl is not explicitly provided.
/// </param>
/// <param name="FunctionKey">
/// The function key used for webhook authentication and should be provided in the request body for security.
/// This key authenticates incoming webhook notifications from Microsoft Graph 
/// (e.g., "abc123def456ghi789jkl012mno345pqr678stu901vwx234yz==").
/// </param>
/// <remarks>
/// This configuration supports flexible webhook setup where:
/// <list type="bullet">
/// <item>Site and List IDs can be omitted if defaults are configured</item>
/// <item>Callback URLs can be auto-generated from function app settings</item>
/// <item>All webhook subscriptions require proper authentication via function keys</item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// var config = new WebhookConfiguration(
///     siteId: "12345678-1234-1234-1234-123456789012",
///     listId: "87654321-4321-4321-4321-210987654321", 
///     callbackUrl: "https://my-app.azurewebsites.net/sharepoint/webhook?code=mykey123",
///     functionAppName: "my-function-app",
///     functionKey: "abc123def456..."
/// );
/// </code>
/// </example>
public record WebhookConfiguration(
    [property:
        OpenApiProperty(Description = "SharePoint site ID in GUID format. Optional if configured in app settings.")]
    string? SiteId,
    [property:
        OpenApiProperty(Description = "SharePoint list ID in GUID format. Optional if configured in app settings.")]
    string? ListId,
    [property: OpenApiProperty(Description = "HTTPS callback URL with authentication. Auto-generated if not provided.")]
    string? CallbackUrl,
    [property: OpenApiProperty(Description = "Function app name for URL generation. Usually auto-detected.")]
    string? FunctionAppName,
    [property:
        OpenApiProperty(Description = "Function authorization key for webhook security. Required for webhook setup.")]
    string? FunctionKey)
{
}