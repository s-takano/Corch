using CorchEdges.Models;

namespace CorchEdges.Abstractions;

public interface ISharePointSyncProcessor
{
    int SuccessfulItems { get; }
    int FailedCount { get; }
    bool HasErrors { get; }
    string? LastError { get; }

    /// Ensures a valid connection to the Microsoft Graph API by verifying connectivity
    /// and handling any connection issues. Logs the status and provides detailed error
    /// information to assist with troubleshooting if the connection attempt fails.
    /// Intended to validate Graph API availability before proceeding with subsequent
    /// operations.
    /// <returns>
    /// A task representing the asynchronous operation. Returns true if the connection
    /// to the Microsoft Graph API is successful, otherwise false.
    /// </returns>
    Task<bool> EnsureGraphConnectionAsync();

    Task<SharePointSyncResult> FetchAndStoreDeltaAsync();
}