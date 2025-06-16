namespace CorchEdges.Models.Requests;

public record WebhookResponse(string Message, string SubscriptionId, DateTimeOffset? ExpirationDateTime, string CallbackUrl, string Resource, string ChangeType, string ClientState)
{
    public override string ToString()
    {
        return $"{{ Message = {Message}, SubscriptionId = {SubscriptionId}, ExpirationDateTime = {ExpirationDateTime}, CallbackUrl = {CallbackUrl}, Resource = {Resource}, ChangeType = {ChangeType}, ClientState = {ClientState} }}";
    }
}