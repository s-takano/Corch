using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;

namespace CorchEdges.Services;

// New records for multiple subscription support
/// <summary>
/// Represents the status of a list of webhooks.
/// </summary>
public record ListWebhookStatus(
    bool HasWebhooks,
    IEnumerable<WebhookSubscriptionInfo> Subscriptions)
{
    /// <summary>
    /// Determines whether the specified callback URL is registered in the list of subscriptions.
    /// This method checks for an existing subscription with a matching callback URL.
    /// </summary>
    /// <param name="expectedUrl">The callback URL to verify in the registered subscriptions.</param>
    /// <returns>
    /// Returns <c>true</c> if a subscription with the specified callback URL exists; otherwise, returns <c>false</c>.
    /// </returns>
    public bool IsRegisteredTo(string expectedUrl) =>
        Subscriptions.Any(s => string.Equals(s.CallbackUrl, expectedUrl, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Gets the total number of elements in the collection.
    /// </summary>
    /// <remarks>
    /// This property provides the count of elements currently present in the collection.
    /// It is often used to determine the size of the collection for iteration or validation purposes.
    /// </remarks>
    /// <value>
    /// An integer representing the number of elements in the collection.
    /// </value>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown if the collection state does not allow access to its count.
    /// </exception>
    public int Count => Subscriptions.Count();

    /// <summary>
    /// Retrieves the webhook subscription associated with the specified callback URL.
    /// This method searches for an existing subscription endpoint matching the provided callback URL.
    /// </summary>
    /// <param name="callbackUrl">The HTTPS URL used as the subscription's callback endpoint.</param>
    /// <returns>The webhook subscription matching the callback URL, or null if no match is found.</returns>
    public WebhookSubscriptionInfo? GetByCallbackUrl(string callbackUrl) =>
        Subscriptions.FirstOrDefault(s =>
            string.Equals(s.CallbackUrl, callbackUrl, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Represents information related to a webhook subscription.
/// This class typically contains details about the webhook endpoint,
/// subscription status, and other metadata related to the subscription process.
/// </summary>
/// 
#pragma warning disable CS0414 // Field is assigned but its value is never used
#pragma warning disable CS0169 // Field is never used
public record WebhookSubscriptionInfo(
    string SubscriptionId,
    string CallbackUrl,
    DateTimeOffset? ExpirationDate,
    string? ClientState = null);
#pragma warning restore CS0414
#pragma warning restore CS0169


/// <summary>
/// Represents the registration details for a webhook integration.
/// </summary>
/// <remarks>
/// This class handles the necessary information to register a webhook,
/// including endpoint, event type, and additional optional details.
/// Webhooks enable real-time notifications of events via HTTP POST requests.
/// </remarks>
public class WebhookRegistration(
    GraphServiceClient graphClient,
    ILogger<WebhookRegistration> logger)
{
    /// <summary>
    /// Represents an instance of a client used to interact with a graph-based API.
    /// This variable is typically initialized to facilitate communication with specific
    /// graph services, including handling requests and processing responses for graph-related operations.
    /// </summary>
    private readonly GraphServiceClient _graphClient =
        graphClient ?? throw new ArgumentNullException(nameof(graphClient));

    /// <summary>
    /// Represents the logging mechanism used to track and record application events, errors, and informational messages.
    /// This variable facilitates centralized logging across the application, allowing for improved debugging
    /// and system monitoring.
    /// </summary>
    private readonly ILogger<WebhookRegistration> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Registers a webhook subscription for a specified SharePoint list.
    /// This method creates a subscription to receive notifications when the list is updated.
    /// </summary>
    /// <param name="siteId">The ID of the SharePoint site that contains the list.</param>
    /// <param name="listId">The ID of the SharePoint list for which the webhook is being registered.</param>
    /// <param name="callbackUrl">
    /// The HTTPS URL that will receive notifications for the subscription.
    /// Must be a valid absolute URL using the HTTPS scheme.
    /// </param>
    /// <param name="clientState">
    /// An optional client-defined string included in notifications to help identify the source.
    /// Defaults to a generated value if not provided.
    /// </param>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the operation.
    /// Defaults to a non-cancelable token if not specified.
    /// </param>
    /// <returns>
    /// Returns a <c>Subscription</c> object representing the created webhook subscription, including its details and expiration date.
    /// </returns>
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
    /// Checks if there is at least one active Microsoft Graph subscription (webhook)
    /// associated with the specified SharePoint list.
    /// A subscription is considered inactive if it is already expired or will expire
    /// within the provided renewal window (default is 30 minutes).
    /// </summary>
    /// <param name="siteId">The ID of the SharePoint site containing the target list.</param>
    /// <param name="listId">The ID of the SharePoint list for which to check webhook subscriptions.</param>
    /// <param name="renewalWindowMinutes">
    /// The time remaining (in minutes) before expiry within which a subscription is considered inactive.
    /// Defaults to 30 minutes.
    /// </param>
    /// <param name="cancellationToken">
    /// Token to monitor for cancellation requests, avoiding unnecessary processing.
    /// </param>
    /// <returns>
    /// A <c>Task</c> resolving to <c>true</c> if at least one active webhook subscription exists for
    /// the specified list, otherwise <c>false</c>.
    /// </returns>
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
        
        if(page is null)
            throw new InvalidOperationException("Graph returned null page.");

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
    /// Retrieves the current status of webhook subscriptions for a specified SharePoint list.
    /// This method checks if active webhook subscriptions exist and provides detailed information about them.
    /// </summary>
    /// <param name="siteId">The unique identifier of the SharePoint site associated with the list.</param>
    /// <param name="listId">The unique identifier of the SharePoint list for which subscriptions are being checked.</param>
    /// <param name="renewalWindowMinutes">
    /// The time window, in minutes, used to determine the validity of subscriptions.
    /// Subscriptions expiring within this window are considered near expiration.
    /// </param>
    /// <param name="cancellationToken">The cancellation token to signal the asynchronous operation should be canceled.</param>
    /// <returns>
    /// Returns a <c>ListWebhookStatus</c> object containing information about the webhook subscriptions,
    /// including whether any active subscriptions exist and details about each subscription.
    /// </returns>
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
    /// Determines whether a specific webhook subscription is registered for a given resource in Microsoft Graph.
    /// This method verifies if a webhook with a specific callback URL is associated with a resource (such as a SharePoint list).
    /// </summary>
    /// <param name="siteId">The identifier of the site containing the resource.</param>
    /// <param name="listId">The identifier of the resource (e.g., a SharePoint list).</param>
    /// <param name="callbackUrl">The callback URL expected to be registered for the webhook. Must be an absolute HTTPS URL.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>
    /// Returns <c>true</c> if a webhook with the specified callback URL is registered for the resource; otherwise, <c>false</c>.
    /// </returns>
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
    /// Retrieves all active webhook subscriptions associated with a specific SharePoint list.
    /// </summary>
    /// <param name="siteId">The identifier of the SharePoint site containing the target list.</param>
    /// <param name="listId">The identifier of the SharePoint list for which subscriptions are queried.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A read-only list of webhook subscriptions targeting the specified SharePoint list.</returns>
    private async Task<IReadOnlyList<Subscription>> GetSubscriptionsForListAsync(
        string siteId,
        string listId,
        CancellationToken cancellationToken = default)
    {
        string resourcePrefix = $"sites/{siteId}/lists/{listId}";
        var result = new List<Subscription>();

        // 1) first request – NO query options
        var page = await _graphClient.Subscriptions.GetAsync(cancellationToken: cancellationToken);
        if(page is null)
            throw new InvalidOperationException("Graph returned null page.");

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
    /// Retrieves all active subscriptions for the current application.
    /// This asynchronous method fetches valid and unexpired subscriptions within
    /// a specified renewal window. Subscriptions are used to track changes or
    /// events for specified resources.
    /// </summary>
    /// <param name="renewalWindowMinutes">
    /// The renewal time window, in minutes, within which active subscriptions are considered.
    /// Must be greater than zero.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests during the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains
    /// a read-only list of active subscriptions.
    /// </returns>
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

            if(page is null)
                throw new InvalidOperationException("Graph returned null page.");
            
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


    /// <summary>
    /// Parses the provided resource string to extract the site and list identifiers.
    /// This method is used to interpret and extract SharePoint site and list information
    /// from a specifically formatted resource string.
    /// </summary>
    /// <param name="resource">The resource string containing the site and list identifiers in the expected format.</param>
    /// <returns>
    /// A tuple containing the extracted site identifier and list identifier as strings.
    /// If the resource string format is invalid or parsing fails, both values in the tuple are <c>null</c>.
    /// </returns>
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
    /// Renews an existing Microsoft Graph webhook subscription for a SharePoint list.
    /// The subscription ID must be valid, and renewal can extend the expiration date by up to 3 days.
    /// </summary>
    /// <param name="subscriptionId">The unique identifier of the subscription to renew.</param>
    /// <param name="daysToExtend">The number of days to extend the subscription. Valid values are between 1 and 3.</param>
    /// <param name="cancellation">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>
    /// Returns a task representing the asynchronous operation. The task result is a boolean indicating whether the renewal was successful.
    /// </returns>
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

    /// <summary>
    /// Deletes an existing Microsoft Graph webhook subscription.
    /// Use this method to remove a subscription and stop receiving notifications.
    /// </summary>
    /// <param name="subscriptionId">The unique identifier of the subscription to delete. This parameter is required.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result indicates whether the deletion was successful.
    /// </returns>
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
    /// Retrieves a list of Microsoft Graph subscriptions that are set to expire within
    /// the specified <paramref name="timeWindow"/> but are still valid.
    /// Defaults the window to 24 hours if not provided. Handles paging and supports
    /// cancellation through <paramref name="cancellation"/>.
    /// </summary>
    /// <param name="timeWindow">The time duration window to filter subscriptions that are nearing expiration. Defaults to 24 hours.</param>
    /// <param name="cancellation">A token to monitor for cancellation requests.</param>
    /// <returns>An asynchronous task that resolves to a read-only list of subscriptions expiring within the specified time window.</returns>
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
            
            if(page is null)
                throw new InvalidOperationException("Graph returned null page.");

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

    /// <summary>
    /// Retrieves a collection of test subscriptions asynchronously.
    /// The returned subscriptions may include temporary or test-specific data
    /// and are primarily intended for development or debugging purposes.
    /// </summary>
    /// <param name="testIdFilter">
    /// An optional filter to narrow down the results to test subscriptions with matching identifiers.
    /// If null, all test subscriptions will be retrieved without filtering.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains a collection of test subscriptions.
    /// </returns>
    public async Task<IEnumerable<Subscription>> GetTestSubscriptionsAsync(string? testIdFilter = null)
    {
        var allSubscriptions = await GetActiveSubscriptionsAsync();

        return allSubscriptions.Where(s =>
            !string.IsNullOrEmpty(s.ClientState) &&
            s.ClientState.StartsWith("integration-test-") &&
            (testIdFilter == null || s.ClientState.Contains(testIdFilter)));
    }

    /// <summary>
    /// Deletes all active webhook subscriptions that were created for testing purposes and match the specified filter.
    /// Test subscriptions are identified by a <c>ClientState</c> value that starts with "integration-test-".
    /// </summary>
    /// <param name="testIdFilter">
    /// An optional filter string to further narrow down which test subscriptions to clean up.
    /// Only subscriptions whose <c>ClientState</c> contains this filter will be considered for deletion.
    /// If <c>null</c>, all test subscriptions are considered.
    /// </param>
    /// <returns>
    /// The number of test subscriptions that were successfully deleted.
    /// </returns>
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

    /// <summary>
    /// Generates a unique client state token useful for ensuring the integrity and validation of client requests.
    /// The generated token can be used to correlate requests and responses securely during interactions.
    /// </summary>
    /// <returns>A string representing the unique client state token.</returns>
    private static string GenerateClientState()
    {
        return Guid.NewGuid().ToString();
    }

    /// <summary>
    /// Stores details of a webhook subscription.
    /// This method logs the subscription details, such as ID, resource, and associated site and list IDs.
    /// It can be extended to persist the subscription details in a database or cache.
    /// </summary>
    /// <param name="subscription">The webhook subscription object containing details such as ID, resource, and client state.</param>
    /// <param name="siteId">The identifier of the SharePoint site associated with the subscription.</param>
    /// <param name="listId">The identifier of the SharePoint list associated with the subscription.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
#pragma warning disable CS0168 // Variable is declared but never used
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
#pragma warning restore CS0168

    /// <summary>
    /// Extracts a detailed error message from the provided exception, if available.
    /// Attempts to access the "Error" and "Message" properties of the exception using reflection,
    /// and falls back to the exception's default message if those properties are not available.
    /// </summary>
    /// <param name="ex">The exception from which to extract the error message.</param>
    /// <returns>The extracted error message, or the default exception message if no specific error message is available.</returns>
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