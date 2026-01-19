using System.ComponentModel.DataAnnotations;

namespace CorchEdges.Data.Entities;

/// <summary>
/// Represents a processing log entry used by SharePointSyncProcessor to track
/// delta synchronization state and processing history for SharePoint lists.
/// </summary>
    public class ProcessingLog
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the SharePoint site ID associated with this processing log.
        /// </summary>
        [Required]
        [StringLength(50)]
        public string SiteId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the SharePoint list ID associated with this processing log.
        /// </summary>
        [Required]
        [StringLength(50)]
        public string ListId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the delta link token returned by SharePoint for tracking incremental changes.
        /// This token is used for subsequent delta queries to get only changed items.
        /// </summary>
        [StringLength(2000)]
        public string? DeltaLink { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the last successful processing run.
        /// </summary>
        public DateTime LastProcessedAt { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when this processing log entry was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when this processing log entry was last updated.
        /// </summary>
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// Gets or sets the number of items processed in the last run.
        /// </summary>
        public int LastProcessedCount { get; set; }

        /// <summary>
        /// Gets or sets the status of the last processing run.
        /// </summary>
        [StringLength(20)]
        public string Status { get; set; } = ProcessingStatus.Pending;

        /// <summary>
        /// Gets or sets any error message from the last processing run.
        /// </summary>
        [StringLength(1000)]
        public string? LastError { get; set; }

        /// <summary>
        /// Gets or sets the subscription ID associated with this processing log.
        /// Links the processing log to the webhook subscription.
        /// </summary>
        [StringLength(100)]
        public string? SubscriptionId { get; set; }

        /// <summary>
        /// Gets or sets the total number of successful processing items.
        /// </summary>
        public int SuccessfulItems { get; set; }

        /// <summary>
        /// Gets or sets the total number of failed processing runs.
        /// </summary>
        public int FailedItems { get; set; }

        // New navigation: one log -> many processed files
        public ICollection<ProcessedFile> ProcessedFiles { get; set; } = new List<ProcessedFile>();
    }

/// <summary>
/// Constants for processing status values.
/// </summary>
public static class ProcessingStatus
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
    public const string Skipped = "Skipped";
}