
namespace CorchEdges.Models;

/// <summary>
/// Represents the result of processing a SharePoint change notification.
/// </summary>
/// <param name="Success">Indicates whether the processing was successful.</param>
/// <param name="ErrorReason">The reason for failure, if any.</param>
/// <param name="ShouldRetry">Indicates whether the operation should be retried by Service Bus.</param>
public record SharePointSyncResult(bool Success, string? ErrorReason = null, bool ShouldRetry = false)
{
    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <returns>A SharePointSyncResult indicating success.</returns>
    public static SharePointSyncResult Succeeded() => new(true);
    
    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="reason">The reason for the failure.</param>
    /// <param name="shouldRetry">Whether the operation should be retried.</param>
    /// <returns>A SharePointSyncResult indicating failure.</returns>
    public static SharePointSyncResult Failed(string reason, bool shouldRetry = false) => new(false, reason, shouldRetry);
}
