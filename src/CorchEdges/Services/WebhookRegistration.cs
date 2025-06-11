using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;

namespace CorchEdges.Services;

// New records for multiple subscription support
public record ListWebhookStatus(
    bool HasWebhooks,
    IEnumerable<WebhookSubscriptionInfo> Subscriptions)
{
    public bool IsRegisteredTo(string expectedUrl) =>
        Subscriptions.Any(s => string.Equals(s.CallbackUrl, expectedUrl, StringComparison.OrdinalIgnoreCase));

    public int Count => Subscriptions.Count();

    public WebhookSubscriptionInfo? GetByCallbackUrl(string callbackUrl) =>
        Subscriptions.FirstOrDefault(s =>
            string.Equals(s.CallbackUrl, callbackUrl, StringComparison.OrdinalIgnoreCase));
}

public record WebhookSubscriptionInfo(
    string SubscriptionId,
    string CallbackUrl,
    DateTimeOffset? ExpirationDate,
    string? ClientState = null);

public class WebhookRegistration(
    GraphServiceClient graphClient,
    ILogger<WebhookRegistration> logger)
{
    private readonly GraphServiceClient _graphClient =
        graphClient ?? throw new ArgumentNullException(nameof(graphClient));

    private readonly ILogger<WebhookRegistration> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Create (or re-use) a Microsoft Graph webhook subscription for a SharePoint list.
    /// • List notifications support only the <c>updated</c> changeType.  
    /// • <paramref name="callbackUrl"/> must be HTTPS.  
    /// • Max lifetime for a list subscription is 3 days; caller should renew before expiry.
    /// </summary>
    public async Task<Subscription> RegisterWebhookAsync(
        string siteId,
        string listId,
        string callbackUrl,
        string? clientState = null,
        CancellationToken cancellationToken = default)
    {
        // ------------------------------------------------------------------
        // 0) Guard clauses & basic validation
        // ------------------------------------------------------------------
        if (string.IsNullOrWhiteSpace(siteId))
            throw new ArgumentException("Site ID cannot be null or empty.", nameof(siteId));

        if (string.IsNullOrWhiteSpace(listId))
            throw new ArgumentException("List ID cannot be null or empty.", nameof(listId));

        if (string.IsNullOrWhiteSpace(callbackUrl))
            throw new ArgumentException("Callback URL cannot be null or empty.", nameof(callbackUrl));

        if (!Uri.TryCreate(callbackUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("Callback URL must be a valid HTTPS URL.", nameof(callbackUrl));

        // ------------------------------------------------------------------
        // 1) Look for an identical active subscription to avoid duplicates
        // ------------------------------------------------------------------
        const string changeType = "updated"; // only allowed for lists
        string resource = $"sites/{siteId}/lists/{listId}";

        var existing = (await GetSubscriptionsForListAsync(siteId, listId, cancellationToken))
            .FirstOrDefault(s =>
                string.Equals(s.NotificationUrl, callbackUrl, StringComparison.OrdinalIgnoreCase)
                && string.Equals(s.Resource, resource, StringComparison.OrdinalIgnoreCase)
                && string.Equals(s.ChangeType, changeType, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            _logger.LogInformation(
                "Re-using subscription {SubscriptionId} for list {ListId} and callback {CallbackUrl}.",
                existing.Id, listId, callbackUrl);
            return existing;
        }

        // ------------------------------------------------------------------
        // 2) Create a new subscription
        // ------------------------------------------------------------------
        string actualClientState = clientState ?? GenerateClientState();
        if (actualClientState.Length > 128) actualClientState = actualClientState[..128]; // Graph limit

        var newSub = new Subscription
        {
            ChangeType = changeType,
            NotificationUrl = callbackUrl,
            Resource = resource,
            ExpirationDateTime = DateTimeOffset.UtcNow.AddDays(3),
            ClientState = actualClientState
        };

        try
        {
            _logger.LogInformation("Registering webhook: resource={Resource}, callback={CallbackUrl}.",
                resource, callbackUrl);

            // v5/v6 Graph SDK overload with token
            var created = await _graphClient.Subscriptions.PostAsync(newSub, null, cancellationToken);

            if (created?.Id is null)
            {
                _logger.LogError("Graph returned null ID – webhook registration failed.");
                throw new InvalidOperationException("Failed to register webhook (no ID).");
            }

            _logger.LogInformation("Webhook registered with ID {SubscriptionId}.", created.Id);

            await StoreSubscriptionDetailsAsync(created, siteId, listId, cancellationToken);
            return created;
        }
        catch (ServiceException ex)
        {
            string msg = GetErrorMessage(ex);
            _logger.LogError(ex, "Graph API error while registering webhook: {Message}", msg);
            throw new InvalidOperationException($"Failed to register webhook: {msg}", ex);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Webhook registration was cancelled.");
            throw;
        }
    }


    /// <summary>
    /// Returns <c>true</c> when there is at least one <strong>active</strong> Microsoft Graph
    /// subscription (web-hook) for the given SharePoint list.
    /// A subscription is considered **inactive** when it
    /// • is already expired, or  
    /// • will expire in <paramref name="renewalWindowMinutes"/> minutes (default = 30).  
    /// </summary>
    /// <remarks>
    /// • Works only with Microsoft Graph web-hooks (you said you don’t need the
    ///   classic SharePoint REST hooks).  
    /// • Relies on the fact that this app is the **only** producer of subscriptions
    ///   (so listing your own `/subscriptions` is sufficient).  
    /// </remarks>
    public async Task<bool> IsListMonitoredByWebhookAsync(
        string siteId,
        string listId,
        int renewalWindowMinutes = 30,
        CancellationToken cancellationToken = default)
    {
        // 0) Validate arguments
        if (string.IsNullOrWhiteSpace(siteId))
            throw new ArgumentException(nameof(siteId));
        if (string.IsNullOrWhiteSpace(listId))
            throw new ArgumentException(nameof(listId));
        if (renewalWindowMinutes <= 0)
            throw new ArgumentOutOfRangeException(nameof(renewalWindowMinutes));

        string listResourcePrefix = $"sites/{siteId}/lists/{listId}";
        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        DateTimeOffset renewalCutoff = nowUtc.AddMinutes(renewalWindowMinutes);

        var activeFound = false;

        // ---- 1) first (and possibly only) page ---------------------------
        var page = await _graphClient.Subscriptions.GetAsync(
            cancellationToken: cancellationToken);

        // ---- 2) iterate all pages & filter locally -----------------------
        var iterator = PageIterator<Subscription, SubscriptionCollectionResponse>
            .CreatePageIterator(
                _graphClient,
                page,
                sub =>
                {
                    if (sub.ExpirationDateTime is { } exp &&
                        exp >= renewalCutoff &&
                        !string.IsNullOrEmpty(sub.Resource) &&
                        sub.Resource.StartsWith(listResourcePrefix,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        activeFound = true;
                        return false; // stop early – we found at least one
                    }

                    return true; // keep paging
                });

        await iterator.IterateAsync(cancellationToken);

        _logger.LogDebug(
            "IsListMonitoredByWebhook(site:{Site}, list:{List}) => {Result} (renewal window {Win} min)",
            siteId, listId, activeFound, renewalWindowMinutes);

        return activeFound;
    }


    /// <summary>
    /// Returns detailed information about Microsoft Graph webhook subscriptions on a
    /// SharePoint list.
    /// A subscription is considered <i>active</i> when its <c>expirationDateTime</c> is
    /// at least <paramref name="renewalWindowMinutes"/> minutes in the future (default 30).
    /// </summary>
    public async Task<ListWebhookStatus> GetListWebhookStatusAsync(
        string siteId,
        string listId,
        int renewalWindowMinutes = 30,
        CancellationToken cancellationToken = default)
    {
        // ------------------------------------------------------------------
        // 0) Parameter validation – throw, don’t hide coding bugs
        // ------------------------------------------------------------------
        if (string.IsNullOrWhiteSpace(siteId))
            throw new ArgumentException("siteId is required.", nameof(siteId));
        if (string.IsNullOrWhiteSpace(listId))
            throw new ArgumentException("listId is required.", nameof(listId));

        var nowUtc = DateTimeOffset.UtcNow;
        var renewalCutoff = nowUtc.AddMinutes(renewalWindowMinutes);

        try
        {
            // ♦ Ensure GetSubscriptionsForListAsync accepts and propagates the token
            var subs = await GetSubscriptionsForListAsync(siteId, listId, cancellationToken);

            var subscriptionInfos = subs.Select(s => new WebhookSubscriptionInfo(
                    s.Id ?? string.Empty,
                    s.NotificationUrl ?? string.Empty,
                    s.ExpirationDateTime,
                    s.ClientState))
                .ToList();

            bool hasActive = subscriptionInfos.Any(i => i.ExpirationDate >= renewalCutoff);

            return new ListWebhookStatus(hasActive, subscriptionInfos);
        }
        catch (ServiceException ex) // Microsoft.Graph
        {
            _logger.LogError(ex,
                "Graph API error while reading subscriptions for site {SiteId}, list {ListId}.",
                siteId, listId);
            throw; // bubble up → caller decides
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("GetListWebhookStatusAsync was cancelled.");
            throw;
        }
    }


    /// <summary>
    /// Returns <c>true</c> when a Microsoft Graph webhook whose
    /// <see cref="Subscription.NotificationUrl"/> equals <paramref name="callbackUrl"/>
    /// (after normalisation) is <em>active</em> on the given SharePoint list.
    /// “Active” is defined by <see cref="GetListWebhookStatusAsync"/> – typically
    /// not expired and not expiring within the renewal window.
    /// </summary>
    public async Task<bool> IsSpecificWebhookRegisteredAsync(
        string siteId,
        string listId,
        string callbackUrl,
        CancellationToken cancellationToken = default)
    {
        // 0) Validate arguments
        if (string.IsNullOrWhiteSpace(siteId))
            throw new ArgumentException("siteId is required.", nameof(siteId));
        if (string.IsNullOrWhiteSpace(listId))
            throw new ArgumentException("listId is required.", nameof(listId));
        if (!Uri.TryCreate(callbackUrl, UriKind.Absolute, out var expectedUri))
            throw new ArgumentException("callbackUrl must be an absolute URL.", nameof(callbackUrl));

        try
        {
            var status = await GetListWebhookStatusAsync(siteId, listId, 30, cancellationToken);

            return status.Subscriptions.Any(sub =>
            {
                if (!Uri.TryCreate(sub.CallbackUrl, UriKind.Absolute, out var actualUri))
                    return false; // malformed in storage – ignore

                // Normalise: case-insensitive host + scheme, ignore default ports,
                // strip trailing slash
                return Uri.Compare(
                    expectedUri, actualUri,
                    UriComponents.Scheme | UriComponents.Host | UriComponents.Port | UriComponents.PathAndQuery,
                    UriFormat.Unescaped,
                    StringComparison.OrdinalIgnoreCase) == 0;
            });
        }
        catch (ServiceException ex) // Graph SDK error
        {
            _logger.LogError(ex,
                "Graph API error while checking webhook for site {SiteId}, list {ListId}.",
                siteId, listId);
            throw; // bubble up – caller decides
        }
    }


    /// <summary>
    /// Gets all active webhook subscriptions for a specific SharePoint list.
    /// </summary>
    /// <param name="siteId">SharePoint site ID</param>
    /// <param name="listId">SharePoint list ID</param>
    /// <returns>Collection of active subscriptions for the list</returns>
    private async Task<IReadOnlyList<Subscription>> GetSubscriptionsForListAsync(
        string siteId,
        string listId,
        CancellationToken cancellationToken = default)
    {
        string resourcePrefix = $"sites/{siteId}/lists/{listId}";
        var result = new List<Subscription>();

        // 1) first request – NO query options
        var page = await _graphClient.Subscriptions.GetAsync(cancellationToken: cancellationToken);

        // 2) walk every page (if Graph gives a nextLink)
        var iterator = PageIterator<Subscription, SubscriptionCollectionResponse>
            .CreatePageIterator(
                _graphClient,
                page,
                sub =>
                {
                    // keep only subscriptions that target this list
                    if (!string.IsNullOrEmpty(sub.Resource) &&
                        sub.Resource.StartsWith(resourcePrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Add(sub);
                    }

                    return true; // continue paging
                });

        await iterator.IterateAsync(cancellationToken);
        return result;
    }


    /// <summary>
    /// Returns every Microsoft Graph subscription that is still <paramref name="renewalWindowMinutes"/>
    /// minutes away from expiry. Handles paging and supports cancellation.
    /// </summary>
    public async Task<IReadOnlyList<Subscription>> GetActiveSubscriptionsAsync(
        int renewalWindowMinutes = 30,
        CancellationToken cancellationToken = default)
    {
        if (renewalWindowMinutes <= 0)
            throw new ArgumentOutOfRangeException(nameof(renewalWindowMinutes));

        _logger.LogInformation(
            "Retrieving active subscriptions (renewal window {Window} min)…", renewalWindowMinutes);

        var nowUtc = DateTimeOffset.UtcNow;
        var cutoff = nowUtc.AddMinutes(renewalWindowMinutes);
        var actives = new List<Subscription>();

        try
        {
            // ---- first page (NO query options allowed) -----------------------
            var page = await _graphClient.Subscriptions.GetAsync(
                cancellationToken: cancellationToken);

            // ---- iterate all pages ------------------------------------------
            var iterator = PageIterator<Subscription, SubscriptionCollectionResponse>
                .CreatePageIterator(
                    _graphClient,
                    page,
                    sub =>
                    {
                        if (sub.ExpirationDateTime is { } exp && exp >= cutoff)
                            actives.Add(sub); // keep “active enough”

                        return true; // continue paging
                    });

            await iterator.IterateAsync(cancellationToken);

            _logger.LogInformation("Found {Count} active subscriptions.", actives.Count);
            return actives; // IList returned as IReadOnlyList
        }
        catch (ServiceException ex) // Microsoft.Graph
        {
            string msg = GetErrorMessage(ex);
            _logger.LogError(ex,
                "Graph API error while retrieving subscriptions: {Message}", msg);
            throw new InvalidOperationException($"Failed to retrieve subscriptions: {msg}", ex);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Retrieving subscriptions was cancelled.");
            throw;
        }
    }


    private static (string? siteId, string? listId) ParseSiteAndList(string? resource)
    {
        if (string.IsNullOrEmpty(resource)) return (null, null);

        // Expected patterns:
        //   sites/{siteId}/lists/{listId}
        //   sites/{siteId}/lists/{listId}/items
        var parts = resource.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 4 && parts[0].Equals("sites", StringComparison.OrdinalIgnoreCase)
                              && parts[2].Equals("lists", StringComparison.OrdinalIgnoreCase))
        {
            return (parts[1], parts[3]);
        }

        return (null, null); // unexpected pattern
    }

    /// <summary>
    /// Extends the <paramref name="subscriptionId"/>'s <c>expirationDateTime</c>
    /// by up to <paramref name="daysToExtend"/> days (max 3 for SharePoint list hooks).
    /// Returns <c>true</c> when Graph confirms the renewal.
    /// </summary>
    public async Task<bool> RenewSubscriptionAsync(
        string subscriptionId,
        int daysToExtend = 3,
        CancellationToken cancellation = default)
    {
        //--------------------------------------------------------------
        // 0) Validate args
        //--------------------------------------------------------------
        if (string.IsNullOrWhiteSpace(subscriptionId))
            throw new ArgumentException("Subscription ID is required.", nameof(subscriptionId));

        if (daysToExtend is < 1 or > 3)
            throw new ArgumentOutOfRangeException(nameof(daysToExtend),
                "SharePoint list subscriptions can be extended 1–3 days only.");

        //--------------------------------------------------------------
        // 1) Build requested expiry (with 1-min safety margin)
        //--------------------------------------------------------------
        var requestedExpiry = DateTimeOffset.UtcNow
            .AddDays(daysToExtend)
            .AddMinutes(-1); // avoid clock-skew rejection

        //--------------------------------------------------------------
        // 2) PATCH
        //--------------------------------------------------------------
        _logger.LogInformation("Renewing subscription {Id} to {NewExpiry}.", subscriptionId, requestedExpiry);

        var patchBody = new Subscription { ExpirationDateTime = requestedExpiry };

        try
        {
            var updated = await _graphClient.Subscriptions[subscriptionId]
                .PatchAsync(patchBody, null, cancellation);

            if (updated?.ExpirationDateTime >= requestedExpiry)
            {
                _logger.LogInformation(
                    "Subscription {Id} renewed until {Exp}.", subscriptionId, updated.ExpirationDateTime);

                var (siteIdParsed, listIdParsed) = ParseSiteAndList(updated.Resource);
                // optionally persist the new details
                await StoreSubscriptionDetailsAsync(updated,
                    siteIdParsed ?? string.Empty,
                    listIdParsed ?? string.Empty,
                    cancellation);

                return true;
            }

            _logger.LogWarning(
                "Graph did not return expected data when renewing subscription {Id}.", subscriptionId);
            return false;
        }
        catch (ODataError ex) when (ex.Error?.Code == "ResourceNotFound")
        {
            _logger.LogWarning(
                "Subscription {{Id}} not found during renewal {Id}: {Message}", subscriptionId, ex.Message);
            return false; 
        }
        catch (ServiceException ex)
        {
            string msg = GetErrorMessage(ex);
            _logger.LogError(ex,
                "Graph API error while renewing subscription {Id}: {Msg}", subscriptionId, msg);
            return false; 
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            _logger.LogWarning("Subscription renewal for {Id} was cancelled.", subscriptionId);
            throw;
        }
    }

    public async Task<bool> DeleteSubscriptionAsync(string subscriptionId)
    {
        if (string.IsNullOrWhiteSpace(subscriptionId))
            throw new ArgumentException("Subscription ID cannot be null or empty", nameof(subscriptionId));

        try
        {
            _logger.LogInformation("Deleting subscription {SubscriptionId}", subscriptionId);

            await _graphClient.Subscriptions[subscriptionId].DeleteAsync();

            _logger.LogInformation("Successfully deleted subscription {SubscriptionId}", subscriptionId);
            return true;
        }
        catch (Exception ex) when (ex.GetType().Name == "ServiceException")
        {
            var errorMessage = GetErrorMessage(ex);
            _logger.LogError(ex, "Graph API error deleting subscription {SubscriptionId}: {ErrorMessage}",
                subscriptionId, errorMessage);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting subscription {SubscriptionId}", subscriptionId);
            return false;
        }
    }

    /// <summary>
    /// Returns every Microsoft Graph subscription that will expire within
    /// <paramref name="timeWindow"/> (default 24 h) but is still valid now.
    /// Handles paging & supports cancellation.
    /// </summary>
    public async Task<IReadOnlyList<Subscription>> GetExpiringSubscriptionsAsync(
        TimeSpan? timeWindow = null,
        CancellationToken cancellation = default)
    {
        // ----- 0) validate / normalise window ---------------------------------
        var window = timeWindow ?? TimeSpan.FromHours(24);
        if (window <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeWindow),
                "timeWindow must be positive.");

        var nowUtc = DateTimeOffset.UtcNow;
        var cutoff = nowUtc.Add(window);

        _logger.LogInformation(
            "Retrieving subscriptions expiring within {Hours} h (cut-off {Cutoff:o}).",
            window.TotalHours, cutoff);

        var expiring = new List<Subscription>();

        try
        {
            // ----- 1) first page — NO query options allowed --------------------
            var page = await _graphClient.Subscriptions.GetAsync(
                cancellationToken: cancellation);

            // ----- 2) iterate all pages ---------------------------------------
            var iterator = PageIterator<Subscription, SubscriptionCollectionResponse>
                .CreatePageIterator(
                    _graphClient,
                    page,
                    sub =>
                    {
                        if (sub.ExpirationDateTime is { } exp &&
                            exp > nowUtc &&
                            exp <= cutoff)
                        {
                            expiring.Add(sub);
                        }

                        return true; // keep iterating
                    });

            await iterator.IterateAsync(cancellation);

            _logger.LogInformation(
                "Found {Count} subscriptions expiring within {Hours} h.",
                expiring.Count, window.TotalHours);

            return expiring;
        }
        catch (ServiceException ex) // Microsoft.Graph
        {
            string msg = GetErrorMessage(ex);
            _logger.LogError(ex,
                "Graph API error while retrieving expiring subscriptions: {Msg}", msg);
            throw new InvalidOperationException(
                $"Failed to retrieve expiring subscriptions: {msg}", ex);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            _logger.LogWarning("Retrieving expiring subscriptions was cancelled.");
            throw;
        }
    }

    public async Task<IEnumerable<Subscription>> GetTestSubscriptionsAsync(string? testIdFilter = null)
    {
        var allSubscriptions = await GetActiveSubscriptionsAsync();

        return allSubscriptions.Where(s =>
            !string.IsNullOrEmpty(s.ClientState) &&
            s.ClientState.StartsWith("integration-test-") &&
            (testIdFilter == null || s.ClientState.Contains(testIdFilter)));
    }

    public async Task<int> CleanupTestSubscriptionsAsync(string? testIdFilter = null)
    {
        var testSubscriptions = await GetTestSubscriptionsAsync(testIdFilter);
        var deletedCount = 0;

        foreach (var subscription in testSubscriptions)
        {
            if (!string.IsNullOrEmpty(subscription.Id))
            {
                var success = await DeleteSubscriptionAsync(subscription.Id);
                if (success) deletedCount++;
            }
        }

        return deletedCount;
    }

    private static string GenerateClientState()
    {
        return Guid.NewGuid().ToString();
    }

    private async Task StoreSubscriptionDetailsAsync(Subscription subscription, string siteId, string listId,
        CancellationToken cancellationToken)
    {
        // This could be extended to store subscription details in a database or cache
        // For now, just log the details
        _logger.LogInformation(
            "Subscription details - ID: {SubscriptionId}, Resource: {Resource}, Site: {SiteId}, List: {ListId}, ClientState: {ClientState}",
            subscription.Id, subscription.Resource, siteId, listId, subscription.ClientState);

        // TODO: Implement persistent storage if needed
        await Task.CompletedTask;
    }

    private static string GetErrorMessage(Exception ex)
    {
        // Use reflection to safely access the Error property without direct reference to ServiceException
        var errorProperty = ex.GetType().GetProperty("Error");
        if (errorProperty?.GetValue(ex) is { } errorObject)
        {
            var messageProperty = errorObject.GetType().GetProperty("Message");
            if (messageProperty?.GetValue(errorObject) is string message)
            {
                return message;
            }
        }

        return ex.Message;
    }
}