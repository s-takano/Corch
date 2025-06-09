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
    public async Task<ConnectionTestResult> TestConnectionAsync()
    {
        try
        {
            // Test with a minimal permissions endpoint
            var servicePrincipal = await graphServiceClient.ServicePrincipals
                .GetAsync(requestConfiguration => { requestConfiguration.QueryParameters.Top = 1; });

            return servicePrincipal?.Value?.Count >= 0
                ? ConnectionTestResult.Success()
                : ConnectionTestResult.Failure("No service principals returned", "EmptyResponse");
        }
        catch (ODataError ex) when (ex.Error?.Code == "Forbidden")
        {
            return ConnectionTestResult.Failure(
                $"Insufficient permissions: {ex.Error?.Message ?? "Access denied"}",
                ex.Error?.Code);
        }
        catch (ODataError ex) when (ex.Error?.Code == "Unauthorized")
        {
            return ConnectionTestResult.Failure(
                $"Authentication failed: {ex.Error?.Message ?? "Invalid credentials"}",
                ex.Error?.Code);
        }
        catch (ODataError ex) when (ex.Error?.Code == "InvalidAuthenticationToken")
        {
            return ConnectionTestResult.Failure(
                $"Invalid token: {ex.Error?.Message ?? "Token expired or malformed"}",
                ex.Error?.Code);
        }
        catch (Azure.Identity.AuthenticationFailedException ex)
        {
            return ConnectionTestResult.Failure(
                $"Credential acquisition failed: {ex.Message}",
                "AuthenticationFailed");
        }
        catch (Azure.RequestFailedException ex)
        {
            return ConnectionTestResult.Failure(
                $"Azure service error: {ex.Message}",
                ex.ErrorCode);
        }
        catch (HttpRequestException ex)
        {
            return ConnectionTestResult.Failure(
                $"Network error: {ex.Message}",
                "NetworkError");
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            return ConnectionTestResult.Failure(
                "Request timeout - Graph API took too long to respond",
                "Timeout");
        }
        catch (Exception ex)
        {
            return ConnectionTestResult.Failure(
                $"Unexpected error: {ex.Message}",
                ex.GetType().Name);
        }
    }
}