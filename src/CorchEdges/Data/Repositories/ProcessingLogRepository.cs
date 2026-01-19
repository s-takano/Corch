using CorchEdges.Data.Abstractions;
using CorchEdges.Data.Entities;
using CorchEdges.Data.Utilities;
using Microsoft.EntityFrameworkCore;

namespace CorchEdges.Data.Repositories;

/// <summary>
/// Repository for managing ProcessingLog entities with business logic.
/// </summary>
public class ProcessingLogRepository(EdgesDbContext context) : IProcessingLogRepository
{
    public async Task<DateTime?> GetLastProcessedAtUtcAsync(string siteId, string listId)
    {
        return await context.QueryProcessingLogsBySiteAndListDesc(siteId, listId)
            .Select(p => p.LastProcessedAt)
            .FirstOrDefaultAsync();
    }

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
    public async Task<string> GetDeltaLinkForSyncAsync(string siteId, string listId)
    {
        var latestDeltaLink = await context.QueryProcessingLogsBySiteAndListDesc(siteId, listId)
            .Where(p => p.DeltaLink != null)
            .Select(p => p.DeltaLink)
            .FirstOrDefaultAsync();
        return latestDeltaLink ?? "latest";
    }

    /// <summary>
    /// Creates or updates a ProcessingLog entry after a successful synchronization.
    /// </summary>
    /// <param name="siteId">The SharePoint site ID.</param>
    /// <param name="listId">The SharePoint list ID.</param>
    /// <param name="deltaLink">The new delta link from SharePoint.</param>
    /// <param name="processedCount">Number of items processed in this sync.</param>
    /// <param name="subscriptionId">Optional subscription ID for webhook tracking.</param>
    /// <returns>The created ProcessingLog entry.</returns>
    public async Task<ProcessingLog> RecordSuccessfulSyncAsync(
        string siteId,
        string listId,
        string deltaLink,
        int processedCount,
        string? subscriptionId = null)
    {
        // Create a new record
        var processingLog = new ProcessingLog
        {
            SiteId = siteId,
            ListId = listId,
            DeltaLink = deltaLink,
            LastProcessedAt = DatabaseDateTime.UtcNow,
            CreatedAt = DatabaseDateTime.UtcNow,
            UpdatedAt = DatabaseDateTime.UtcNow,
            LastProcessedCount = processedCount,
            Status = ProcessingStatus.Completed,
            SuccessfulItems = processedCount,
            SubscriptionId = subscriptionId
        };

        context.ProcessingLogs.Add(processingLog);

        await context.SaveChangesAsync();
        return processingLog;
    }

    /// <summary>
    /// Records a failed synchronization attempt.
    /// </summary>
    /// <param name="siteId">The SharePoint site ID.</param>
    /// <param name="listId">The SharePoint list ID.</param>
    /// <param name="errorMessage">The error message describing the failure.</param>
    /// <param name="failedItems">The number of failed items</param>
    /// <param name="subscriptionId">Optional subscription ID for webhook tracking.</param>
    /// <returns>The created ProcessingLog entry.</returns>
    public async Task<ProcessingLog> RecordFailedSyncAsync(
        string siteId,
        string listId,
        string errorMessage,
        int failedItems,
        string? subscriptionId = null)
    {
        // Create new record even for failures
        var existing = new ProcessingLog
        {
            SiteId = siteId,
            ListId = listId,
            CreatedAt = DatabaseDateTime.UtcNow,
            UpdatedAt = DatabaseDateTime.UtcNow,
            Status = ProcessingStatus.Failed,
            LastError = errorMessage,
            FailedItems = failedItems,
            SubscriptionId = subscriptionId
        };

        context.ProcessingLogs.Add(existing);


        await context.SaveChangesAsync();
        return existing;
    }

    /// <summary>
    /// Gets recent processing history for a specific site and list.
    /// </summary>
    /// <param name="siteId">The SharePoint site ID.</param>
    /// <param name="listId">The SharePoint list ID.</param>
    /// <param name="count">Number of recent entries to retrieve (default: 10).</param>
    /// <returns>List of recent ProcessingLog entries.</returns>
    public async Task<List<ProcessingLog>> GetRecentProcessingHistoryAsync(
        string siteId,
        string listId,
        int count = 10)
    {
        return await context
            .QueryProcessingLogsBySiteAndListDesc(siteId, listId)
            .Take(count)
            .ToListAsync();
    }
}