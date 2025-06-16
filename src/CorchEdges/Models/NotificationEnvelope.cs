using System.Text.Json.Serialization;

namespace CorchEdges.Models;

/// <summary>
/// Represents a container for SharePoint webhook change notifications.
/// </summary>
public sealed class NotificationEnvelope
{
    [JsonPropertyName("value")] public SharePointNotification[] Value { get; set; } = [];
}

/// <summary>
/// Represents a SharePoint webhook change notification.
/// </summary>
public class SharePointNotification
{
    [JsonPropertyName("subscriptionId")] public string SubscriptionId { get; set; } = string.Empty;

    [JsonPropertyName("clientState")] public string ClientState { get; set; } = string.Empty;

    [JsonPropertyName("resource")] public string Resource { get; set; } = string.Empty;

    [JsonPropertyName("tenantId")] public string TenantId { get; set; } = string.Empty;

    [JsonPropertyName("resourceData")] public ResourceData ResourceData { get; set; } = new();

    [JsonPropertyName("subscriptionExpirationDateTime")]
    public DateTimeOffset SubscriptionExpirationDateTime { get; set; }

    [JsonPropertyName("changeType")] public string ChangeType { get; set; } = string.Empty;
}

/// <summary>
/// Represents the resource data in a SharePoint notification.
/// </summary>
public class ResourceData
{
    [JsonPropertyName("@odata.type")] public string ODataType { get; set; } = string.Empty;
}