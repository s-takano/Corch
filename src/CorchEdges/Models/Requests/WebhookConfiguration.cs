namespace CorchEdges.Models.Requests;

/// Represents the configuration details required to set up a SharePoint webhook.
/// This class is used to encapsulate the essential data needed for webhook registration and operation.
public class WebhookConfiguration(string? siteId, string? listId, string? callbackUrl, string? functionAppName)
{
    /// <summary>
    /// Gets the unique identifier for the SharePoint site associated with
    /// the webhook configuration. This property is essential to specify
    /// the target SharePoint site where the webhook will be set up or managed.
    /// </summary>
    public string? SiteId { get; init; } = siteId;

    /// Gets or sets the identifier of the SharePoint list for which the webhook is being set up.
    /// This property is used to uniquely identify the list within a SharePoint site and is required
    /// for registering or checking the existence of a webhook associated with the list.
    public string? ListId { get; init; } = listId;

    /// <summary>
    /// Gets or sets the callback URL where the webhook notifications will be sent.
    /// </summary>
    /// <remarks>
    /// The callback URL must be a valid HTTPS endpoint and must include any necessary authentication parameters.
    /// It is used during webhook registration to specify the destination for notification events.
    /// Proper validation of this URL is crucial to ensure secure and reliable communication.
    /// </remarks>
    public string? CallbackUrl { get; init; } = callbackUrl;

    /// <summary>
    /// Gets the name of the Azure Function App associated with the webhook.
    /// </summary>
    /// <remarks>
    /// This property is used to store or retrieve the name of the Azure Function App
    /// that is responsible for handling webhook callback requests.
    /// </remarks>
    public string? FunctionAppName { get; init; } = functionAppName;
}