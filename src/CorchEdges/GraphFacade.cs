using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;

namespace CorchEdges;


public sealed class GraphFacade(GraphServiceClient graphServiceClient) : IGraphFacade
{
    public Task<ListItem?> GetListItemAsync(string site, string list, string itm) =>
        graphServiceClient.Sites[site].Lists[list].Items[itm].GetAsync(o => o.QueryParameters.Expand = ["fields"]);
        
    public Task<DriveItem?> GetDriveItemAsync(string site, string list, string itm) =>
        graphServiceClient.Sites[site].Lists[list].Items[itm].DriveItem.GetAsync();
        
    public async Task<Stream> DownloadAsync(string driveId, string itemId) =>
        await graphServiceClient.Drives[driveId].Items[itemId].Content.GetAsync() ?? 
        throw new InvalidOperationException("null stream");

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