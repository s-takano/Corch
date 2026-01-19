using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;

namespace CorchEdges.Data.Entities;

public class ProcessedFile
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? SharePointItemId { get; set; }
    public DateTime ProcessedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public int RecordCount { get; set; }
    
    /// <summary>
    /// Gets or sets the SHA-256 hash of the file content.
    /// Used to detect duplicate downloads and ensure file integrity.
    /// </summary>
    [StringLength(64)] // SHA-256 produces 64 character hex string
    public string? FileHash { get; set; }
    
    /// <summary>
    /// Gets or sets the size of the file in bytes.
    /// Used in conjunction with hash for duplicate detection.
    /// </summary>
    public long? FileSizeBytes { get; set; }

    public int ProcessingLogId { get; set; }
    public ProcessingLog ProcessingLog { get; set; } = null!;

    // Inverse navigations to records produced from this file
    public ICollection<ContractCreation> ContractCreations { get; set; } = new List<ContractCreation>();
    public ICollection<ContractCurrent> ContractCurrents { get; set; } = new List<ContractCurrent>();
    public ICollection<ContractRenewal> ContractRenewals { get; set; } = new List<ContractRenewal>();
    public ICollection<ContractTermination> ContractTerminations { get; set; } = new List<ContractTermination>();
}