using Microsoft.Azure.Functions.Worker.Extensions.OpenApi.Extensions;
using System.ComponentModel.DataAnnotations;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;

namespace CorchEdges.Models;

/// <summary>
/// Represents the result of processing a SharePoint change notification.
/// </summary>
/// <param name="Success">Indicates whether the processing was successful.</param>
/// <param name="ErrorReason">The reason for failure, if any.</param>
/// <param name="ShouldRetry">Indicates whether the operation should be retried by Service Bus.</param>
public record SharePointSyncResult(
    [property: OpenApiProperty(Description = "Indicates whether the SharePoint synchronization operation completed successfully")]
    [property: Required]
    bool Success, 
    
    [property: OpenApiProperty(Description = "Human-readable description of the error that occurred during processing. Only populated when Success is false.")]
    string? ErrorReason = null, 
    
    [property: OpenApiProperty(Description = "Indicates whether the failed operation should be automatically retried by the Service Bus. Used for transient failures like network issues.")]
    bool ShouldRetry = false)
{
    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <returns>A SharePointSyncResult indicating success.</returns>
    [OpenApiIgnore]
    public static SharePointSyncResult Succeeded() => new(true);
    
    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="reason">The reason for the failure.</param>
    /// <param name="shouldRetry">Whether the operation should be retried.</param>
    /// <returns>A SharePointSyncResult indicating failure.</returns>
    [OpenApiIgnore]
    public static SharePointSyncResult Failed(string reason, bool shouldRetry = false) => new(false, reason, shouldRetry);
}