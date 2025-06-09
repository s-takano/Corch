using System.Text.Json.Serialization;
using Microsoft.Graph.Models;

namespace CorchEdges.Models;

/// <summary>
/// Represents a container for Microsoft Graph change notifications, typically
/// deserialized from a Service Bus message. This class encapsulates the incoming
/// notifications and provides a strongly-typed structure to process them.
/// </summary>
public sealed class NotificationEnvelope
{
    /// Represents an array of change notifications received in the notification envelope.
    /// This property contains the notifications for changes detected in tracked resources.
    /// Each notification provides information such as the resource that was changed
    /// and the type of change (e.g., created, updated, or deleted).
    [JsonPropertyName("value")] public ChangeNotification[] Value { get; set; } = [];
}