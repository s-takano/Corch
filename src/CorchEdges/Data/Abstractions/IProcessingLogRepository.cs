using CorchEdges.Data.Entities;

namespace CorchEdges.Data.Abstractions;

public interface IProcessingLogRepository
{
    /// <summary>
    /// Gets the latest delta link for SharePoint synchronization.
    /// Returns "latest" as fallback if no previous delta link exists.
    /// </summary>
    /// <param name="siteId">The SharePoint site ID.</param>
    /// <param name="listId">The SharePoint list ID.</param>
    /// <returns>
    /// The most recent delta link, or "latest" if none exists.
    /// This "latest" value is used by SharePoint to create a new baseline.
    /// </returns>
    Task<string> GetDeltaLinkForSyncAsync(string siteId, string listId);

    /// <summary>
    /// Creates or updates a ProcessingLog entry after a successful synchronization.
    /// </summary>
    /// <param name="siteId">The SharePoint site ID.</param>
    /// <param name="listId">The SharePoint list ID.</param>
    /// <param name="deltaLink">The new delta link from SharePoint.</param>
    /// <param name="processedCount">Number of items processed in this sync.</param>
    /// <param name="subscriptionId">Optional subscription ID for webhook tracking.</param>
    /// <returns>The created ProcessingLog entry.</returns>
    Task<ProcessingLog> RecordSuccessfulSyncAsync(
        string siteId,
        string listId,
        string deltaLink,
        int processedCount,
        string? subscriptionId = null);

    /// <summary>
    /// Records a failed synchronization attempt.
    /// </summary>
    /// <param name="siteId">The SharePoint site ID.</param>
    /// <param name="listId">The SharePoint list ID.</param>
    /// <param name="errorMessage">The error message describing the failure.</param>
    /// <param name="failedItems">The number of failed items</param>
    /// <param name="subscriptionId">Optional subscription ID for webhook tracking.</param>
    /// <returns>The created ProcessingLog entry.</returns>
    Task<ProcessingLog> RecordFailedSyncAsync(
        string siteId,
        string listId,
        string errorMessage,
        int failedItems,
        string? subscriptionId = null);

    /// <summary>
    /// Gets recent processing history for a specific site and list.
    /// </summary>
    /// <param name="siteId">The SharePoint site ID.</param>
    /// <param name="listId">The SharePoint list ID.</param>
    /// <param name="count">Number of recent entries to retrieve (default: 10).</param>
    /// <returns>List of recent ProcessingLog entries.</returns>
    Task<List<ProcessingLog>> GetRecentProcessingHistoryAsync(
        string siteId,
        string listId,
        int count = 10);
}