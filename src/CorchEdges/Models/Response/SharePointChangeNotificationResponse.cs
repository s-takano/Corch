using Microsoft.Azure.Functions.Worker.Http;

namespace CorchEdges.Models.Response;

/// <summary>
/// Represents the output bindings for the SharePoint webhook function.
/// Combines both Service Bus message output and HTTP response in a single immutable record.
/// </summary>
/// <param name="BusMessage">
/// Optional JSON message to be sent to the "sp-changes" Service Bus queue.
/// Contains serialized notification data when a change notification is received.
/// Null during validation handshake or error scenarios.
/// </param>
/// <param name="HttpResponse">
/// HTTP response to be returned to SharePoint. Contains the validation token during handshake
/// or acknowledgment response for change notifications.
/// </param>
public record SharePointChangeNotificationResponse(
    [property: ServiceBusOutput("sp-changes",
        Connection = "ServiceBusConnection")]
    string? BusMessage,
    HttpResponseData HttpResponse);