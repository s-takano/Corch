using CorchEdges.Abstractions;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;

namespace CorchEdges.Utilities;

/// <summary>
/// Provides a facade for interacting with Microsoft Graph API services.
/// </summary>
/// <remarks>
/// This class encapsulates common operations on Microsoft Graph services,
/// such as working with SharePoint lists, OneDrive items, downloading files,
/// and testing connectivity. It provides a simplified interface for making
/// requests and handling responses from the Microsoft Graph API.
/// </remarks>
public sealed class GraphFacade(GraphServiceClient graphServiceClient) : IGraphFacade
{
    /// <summary>
    /// Retrieves a list item from a specific SharePoint list within a specified site.
    /// </summary>
    /// <param name="site">
    /// The unique identifier of the SharePoint site.
    /// </param>
    /// <param name="list">
    /// The unique identifier of the SharePoint list within the site.
    /// </param>
    /// <param name="itm">
    /// The unique identifier of the list item to be retrieved.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation. The task result contains the retrieved list item,
    /// or <c>null</c> if the item is not found.
    /// </returns>
    public Task<ListItem?> GetListItemAsync(string site, string list, string itm) =>
        graphServiceClient.Sites[site].Lists[list].Items[itm].GetAsync(o => o.QueryParameters.Expand = ["fields"]);

    /// Asynchronously retrieves a drive item from a specific site, list, and item in Microsoft Graph API.
    /// <param name="site">The identifier of the site containing the drive item.</param>
    /// <param name="list">The identifier of the list containing the drive item.</param>
    /// <param name="itm">The identifier of the item within the list that contains the drive item.</param>
    /// <returns>A task representing the asynchronous operation, containing the retrieved <see cref="DriveItem"/>.
    /// Returns null if the drive item does not exist.</returns>
    public Task<DriveItem?> GetDriveItemAsync(string site, string list, string itm) =>
        graphServiceClient.Sites[site].Lists[list].Items[itm].DriveItem.GetAsync();

    /// <summary>
    /// Downloads the content of a drive item as a stream from Microsoft Graph.
    /// </summary>
    /// <param name="driveId">The identifier of the drive containing the item to download.</param>
    /// <param name="itemId">The identifier of the item to download from the drive.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the content of the drive item as a <see cref="Stream"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the returned content stream is null.</exception>
    public async Task<Stream> DownloadAsync(string driveId, string itemId) =>
        await graphServiceClient.Drives[driveId].Items[itemId].Content.GetAsync() ??
        throw new InvalidOperationException("null stream");

    /// Tests the connection to the Graph API using the GraphServiceClient instance.
    /// Returns a result indicating whether the connection test was successful or failed,
    /// including any error details in case of failure.
    /// <returns>A ConnectionTestResult indicating the success status of the test and any relevant error details.</returns>
    public async Task<ConnectionTestResult> TestConnectionAsync(string siteId = "root")
    {
        try
        {
            // requires only Sites.Read.All
            var site = await graphServiceClient.Sites[siteId].GetAsync();

            return site != null
                ? ConnectionTestResult.Success()
                : ConnectionTestResult.Failure("Site not found", "NotFound");
        }
        catch (ODataError ex) when (ex.Error?.Code is "Forbidden" or "AccessDenied")
        {
            return ConnectionTestResult.Failure(
                $"Insufficient permissions: {ex.Error?.Message}", ex.Error?.Code);
        }
        catch (Exception ex)
        {
            return ConnectionTestResult.Failure($"Unexpected error: {ex.Message}", ex.GetType().Name);
        }
    }
    /// <summary>
    /// Returns every list item created/updated/deleted since the
    /// <paramref name="cursor"/> and hands back the new delta link.
    ///
    /// Pass <c>null</c> or the literal <c>"latest"</c> **exactly once**
    /// to create a “start-from-now” watermark without downloading the
    /// whole list.  Thereafter persist and reuse the link that comes back
    /// in <c>deltaLink</c>.
    /// </summary>
    /// <param name="siteId">Site ID (GUID or host,guid,guid notation).</param>
    /// <param name="listId">List GUID.</param>
    /// <param name="cursor">
    ///     • <c>null</c>       → first boot, create baseline  
    ///     • <c>"latest"</c>  → explicit baseline (same effect as <c>null</c>)  
    ///     • deltaLink URL   → normal incremental poll
    /// </param>
    /// <returns>
    /// (<b>deltaLink</b>, <b>changes</b>) where  
    /// - <c>deltaLink</c> is the bookmark to store for the next round.  
    /// - <c>changes</c> is every <see cref="ListItem"/> changed in this round.
    /// </returns>
    public async Task<(string deltaLink, List<string> itemIds)> PullItemsDeltaAsync(
        string siteId,
        string listId,
        string? cursor /* null / "latest" → bootstrap ; deltaLink → normal */)
    {
        // Builder for …/sites/{siteId}/lists/{listId}/items/delta
        var deltaBuilder = graphServiceClient
            .Sites[siteId]
            .Lists[listId]
            .Items
            .Delta;

        var itemIds = new List<string>();
        string? hop = null; // next URL to request
        string? mark = null; // final @odata.deltaLink

        // ── 1) Bootstrap or incremental kick-off ──────────────────────────
        if (string.IsNullOrEmpty(cursor) ||
            cursor.Equals("latest", StringComparison.OrdinalIgnoreCase))
        {
            // Manually craft “…/delta?token=latest”
            var bootstrapUrl = $"{graphServiceClient.RequestAdapter.BaseUrl}/" +
                               $"sites/{siteId}/lists/{listId}/items/delta?token=latest";

            var seed = await deltaBuilder
                .WithUrl(bootstrapUrl)
                .GetAsDeltaGetResponseAsync();

            mark = seed.OdataDeltaLink; // bookmark “now”
            hop = seed.OdataNextLink; // almost always null
        }
        else
        {
            hop = cursor; // previously stored deltaLink
        }

        // ── 2) Walk every @odata.nextLink in this delta round ─────────────
        while (hop != null)
        {
            var page = await deltaBuilder
                .WithUrl(hop) // nextLink OR deltaLink
                .GetAsDeltaGetResponseAsync();

            if (page.Value?.Count > 0)
                itemIds.AddRange(page.Value.Select(i => i.Id)!);

            if (!string.IsNullOrEmpty(page.OdataNextLink))
            {
                hop = page.OdataNextLink; // keep paging
            }
            else
            {
                mark = page.OdataDeltaLink
                       ?? throw new InvalidOperationException("Missing @odata.deltaLink");
                hop = null; // round finished
            }
        }

        return (mark!, itemIds);
    }
}