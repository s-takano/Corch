namespace CorchEdges.Data.Entities;

public class ContractTermination
{
    public int Id { get; set; }
    
    // Basic Contract Information
    public string? ContractId { get; set; }
    public int? PropertyNo { get; set; }
    public int? RoomNo { get; set; }
    public int? ContractorNo { get; set; }
    public string? PropertyName { get; set; }
    public string? ContractorName { get; set; }
    public string? RoomType { get; set; }
    
    // Status and Key Dates
    public string? ProgressStatus { get; set; }
    public DateOnly? ApplicationDate { get; set; }
    public DateOnly? NotificationReceiptDate { get; set; }
    public DateOnly? ScheduledMoveOutDate { get; set; }
    public DateOnly? ChangeMonth { get; set; }
    public DateOnly? ActualMoveOutDate { get; set; }
    public DateOnly? ProrationDate { get; set; }
    
    // Inspection Process
    public DateOnly? MoveOutInspectionDate { get; set; }
    public string? MoveOutInspector { get; set; }
    
    // Settlement Process
    public DateOnly? SettlementCreationDate { get; set; }
    public string? SettlementCreator { get; set; }
    public DateOnly? MeetingDate { get; set; }
    public DateOnly? FinalApprovalDate { get; set; }
    public DateOnly? SettlementSentDate { get; set; }
    public string? SettlementSender { get; set; }
    
    // Financial Settlement
    public DateOnly? SettlementPaymentDate { get; set; }
    public DateOnly? ReverseChargePaymentDate { get; set; }
    
    // Document Management
    public DateOnly? DocumentScanDate { get; set; }
    
    public string? ConstructionNumber { get; set; }
    
    // Metadata
    public DateTime? OutputDateTime { get; set; }
 
    public int ProcessedFileId { get; set; }
    public ProcessedFile? ProcessedFile { get; set; }
}