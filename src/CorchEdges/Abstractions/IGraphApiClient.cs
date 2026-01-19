using Microsoft.Graph.Models;

namespace CorchEdges.Abstractions;

/// <summary>
/// Represents the result of a connection test.
/// </summary>
public sealed record ConnectionTestResult(
    bool IsSuccess,
    string? ErrorReason = null,
    string? ErrorCode = null)
{
    /// Creates a successful instance of the ConnectionTestResult.
    /// <return>
    /// A ConnectionTestResult instance representing a successful connection test.
    /// </return>
    public static ConnectionTestResult Success() => new(true);

    /// Creates a failed result for a connection test.
    /// <param name="reason">The reason for the failure.</param>
    /// <param name="code">An optional error code representing the failure.</param>
    /// <return>A new <see cref="ConnectionTestResult"/> indicating failure.</return>
    public static ConnectionTestResult Failure(string reason, string? code = null) => new(false, reason, code);
}

/// <summary>
/// Defines methods for interacting with Microsoft Graph services,
/// such as retrieving SharePoint list items, accessing drive items,
/// downloading files, and testing connectivity to Microsoft Graph.
/// </summary>
public interface IGraphApiClient
{
    /// Retrieves a specific list item from a SharePoint site using its site ID, list ID, and item ID.
    /// <param name="siteId">The unique identifier of the SharePoint site containing the list.</param>
    /// <param name="listId">The unique identifier of the list within the SharePoint site.</param>
    /// <param name="itemId">The unique identifier of the item within the list.</param>
    /// <returns>A task representing the asynchronous operation. The task's result contains the retrieved ListItem object, or null if the item does not exist.</returns>
    Task<ListItem?> GetListItemAsync(string siteId, string listId, string itemId);

    /// <summary>
    /// Asynchronously retrieves a drive item from a specified site, list, and item ID in Microsoft Graph.
    /// </summary>
    /// <param name="siteId">The unique identifier of the site containing the drive item.</param>
    /// <param name="listId">The unique identifier of the list containing the drive item.</param>
    /// <param name="itemId">The unique identifier of the item within the list.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the <see cref="DriveItem"/> object if found; otherwise, null.</returns>
    Task<DriveItem?> GetDriveItemAsync(string siteId, string listId, string itemId);

    /// <summary>
    /// Downloads the content of a drive item as a stream from the specified drive and item identifiers.
    /// </summary>
    /// <param name="driveId">The unique identifier of the drive containing the item to download.</param>
    /// <param name="driveItemId">The unique identifier of the item to download within the specified drive.</param>
    /// <returns>A <see cref="Stream"/> representing the content of the specified drive item.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the content stream is null.</exception>
    Task<Stream> DownloadAsync(string driveId, string driveItemId);

    /// <summary>
    /// Asynchronously tests the connection to the Graph API service and determines its status.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result is a <see cref="ConnectionTestResult"/>
    /// containing the outcome of the connection test, including success status, error reason, and error code if applicable.
    /// </returns>
    Task<ConnectionTestResult> TestConnectionAsync(string siteId = "root");

    Task<(string deltaLink, List<string> itemIds)> PullItemsDeltaAsync(string siteId, string listId, string? lastDeltaLink = null);

    
    /// <summary>
    /// Pull items modified since a given UTC timestamp.
    /// </summary>
    Task<List<string>> PullItemsModifiedSinceAsync(string siteId, string listId, DateTime sinceUtc);

    /// <summary>
    /// Get a fresh delta link without downloading all items.
    /// </summary>
    Task<string> GetFreshDeltaLinkAsync(string siteId, string listId);
}