using Microsoft.Graph.Models;

namespace CorchEdges;

public sealed record ConnectionTestResult(
    bool IsSuccess,
    string? ErrorReason = null,
    string? ErrorCode = null)
{
    public static ConnectionTestResult Success() => new(true);
    public static ConnectionTestResult Failure(string reason, string? code = null) => new(false, reason, code);
}

public interface IGraphFacade
{
    Task<ListItem?> GetListItemAsync(string siteId, string listId, string itemId);
    Task<DriveItem?> GetDriveItemAsync(string siteId, string listId, string itemId);
    Task<Stream> DownloadAsync(string driveId, string driveItemId);
    Task<ConnectionTestResult> TestConnectionAsync(); // Updated return type
}