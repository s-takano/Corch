using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;

namespace CorchEdges.Models;

/// <summary>
/// Represents a container for SharePoint webhook change notifications.
/// </summary>
public sealed class NotificationEnvelope
{
    [OpenApiProperty(Description = "Array of SharePoint change notifications")]
    [Required]
    [JsonPropertyName("value")] public SharePointNotification[] Value { get; set; } = [];
}

/// <summary>
/// Represents a SharePoint webhook change notification.
/// </summary>
public class SharePointNotification
{
    [OpenApiProperty(Description = "Unique identifier for this notification")]
    [JsonPropertyName("subscriptionId")] 
    public string SubscriptionId { get; set; } = string.Empty;

    [OpenApiProperty(Description = "Client state value for webhook validation")]
    [JsonPropertyName("clientState")] 
    public string ClientState { get; set; } = string.Empty;

    [OpenApiProperty(Description = "SharePoint resource that changed (e.g., 'sites/{siteId}/lists/{listId}/items/{itemId}')")]
    [JsonPropertyName("resource")] 
    public string Resource { get; set; } = string.Empty;

    [OpenApiProperty(Description = "Tenant identifier")]
    [JsonPropertyName("tenantId")] 
    public string TenantId { get; set; } = string.Empty;

    [JsonPropertyName("resourceData")] public ResourceData ResourceData { get; set; } = new();

    [OpenApiProperty(Description = "Expiration time of the subscription")]
    [JsonPropertyName("subscriptionExpirationDateTime")]
    public DateTimeOffset SubscriptionExpirationDateTime { get; set; }

    [OpenApiProperty(Description = "Type of change that occurred (Created, Updated, Deleted)")]
    [JsonPropertyName("changeType")] 
    public string ChangeType { get; set; } = string.Empty;
}

/// <summary>
/// Represents the resource data in a SharePoint notification.
/// </summary>
public class ResourceData
{
    [JsonPropertyName("@odata.type")] public string ODataType { get; set; } = string.Empty;
}