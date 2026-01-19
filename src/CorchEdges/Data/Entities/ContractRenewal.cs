namespace CorchEdges.Data.Entities;

public class ContractRenewal
{
    public int Id { get; set; }
    
    // Basic Contract Information
    public string? ContractId { get; set; }
    public int? PropertyNo { get; set; }
    public int? RoomNo { get; set; }
    public int? ContractorNo { get; set; }
    public string? PropertyName { get; set; }
    public string? ContractorName { get; set; }
    
    // Status and Dates
    public string? ProgressStatus { get; set; }
    public DateOnly? RenewalDate { get; set; }
    public DateOnly? PreviousContractStartDate { get; set; }
    public DateOnly? PreviousContractEndDate { get; set; }
    public DateOnly? NextContractStartDate { get; set; }
    public DateOnly? NextContractEndDate { get; set; }
    public DateOnly? ContractRenewalDate { get; set; }
    
    // Metadata
    public DateTime? OutputDateTime { get; set; }

    public int ProcessedFileId { get; set; }
    public ProcessedFile? ProcessedFile { get; set; }
}