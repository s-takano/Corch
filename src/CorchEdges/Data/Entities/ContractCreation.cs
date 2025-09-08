namespace CorchEdges.Data.Entities;

public class ContractCreation
{
    public int Id { get; set; }
    public string? ContractId { get; set; }
    public int? PropertyNo { get; set; }
    public int? RoomNo { get; set; }
    public int? ContractorNo { get; set; }
    public string? PropertyName { get; set; }
    public string? ContractorName { get; set; }
    public string? ProgressStatus { get; set; }
    public string? ContractStatus { get; set; }
    public DateOnly? ApplicationDate { get; set; }
    public DateOnly? MoveInDate { get; set; }
    public DateOnly? KeyHandoverDate { get; set; }
    public DateOnly? ContractDate { get; set; }
    
    public decimal? KeyMoney { get; set; }
    public decimal? BrokerageCommission { get; set; }
    public string? LeadSource { get; set; }
    public string? LeadSourceDetail { get; set; }
    public string? AccountManager { get; set; }
    public DateOnly? SupervisorApprovalDate { get; set; }
    public decimal? SecurityDeposit { get; set; }
    public decimal? BrokerageFee { get; set; }
    public decimal? GuaranteeFee { get; set; }
    public decimal? ApartmentInsurance { get; set; }
    public decimal? KeyReplacementFee { get; set; }
    public decimal? DocumentStampFee { get; set; }
    public decimal? WithdrawalFee { get; set; }
    public decimal? BicycleRegistrationFee { get; set; }
    public decimal? MotorcycleRegistrationFee { get; set; }
    public decimal? InternetApplicationFee { get; set; }
    public decimal? MaximumAmount { get; set; }
    public DateTime? OutputDateTime { get; set; }
    
    public int ProcessedFileId { get; set; }
    public ProcessedFile? ProcessedFile { get; set; }
}