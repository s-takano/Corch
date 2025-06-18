
using CorchEdges.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CorchEdges.Data;

/// <summary>
/// ProcessingLog-specific query methods for EdgesDbContext.
/// </summary>
public partial class EdgesDbContext
{
    /// <summary>
    /// Returns a queryable collection of ProcessingLogs filtered by SiteId and ListId,
    /// ordered by CreatedAt descending (most recent first).
    /// </summary>
    /// <param name="siteId">The SharePoint site ID to filter by.</param>
    /// <param name="listId">The SharePoint list ID to filter by.</param>
    /// <returns>An IQueryable that can be further composed before execution.</returns>
    public IQueryable<ProcessingLog> QueryProcessingLogsBySiteAndListDesc(string siteId, string listId)
    {
        return ProcessingLogs
            .Where(p => p.SiteId == siteId && p.ListId == listId)
            .OrderByDescending(p => p.CreatedAt);
    }
}