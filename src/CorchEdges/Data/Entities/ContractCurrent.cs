
namespace CorchEdges.Data.Entities;

public class ContractCurrent
{
    public int Id { get; set; }
    
    // Basic Contract Information
    public string? ContractId { get; set; }
    public string? ResidentCode { get; set; }
    public string? ContractTypeName { get; set; }
    public string? PropertyName { get; set; }
    public string? RoomType { get; set; }
    public int? PropertyNo { get; set; }
    public int? RoomNo { get; set; }
    public int? ContractorNo { get; set; }
    
    // Names
    public string? ContractorName { get; set; }
    public string? ResidentName { get; set; }
    public string? GuarantorName { get; set; }
    public string? EmergencyContactName { get; set; }
    
    // Contract Status and Dates
    public string? ContractStatus { get; set; }
    public DateOnly? CancellationDate { get; set; }
    public DateOnly? ContractRevisionDate { get; set; }
    public DateOnly? MoveInDate { get; set; }
    public DateOnly? ContractStartDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public int? NoticeInDays { get; set; }
    
    // Monthly Fees
    public decimal? Rent { get; set; }
    public decimal? ManagementFee { get; set; }
    public decimal? WaterFee { get; set; }
    public decimal? HotWaterFee { get; set; }
    public decimal? ElectricityFee { get; set; }
    public decimal? PhoneFee { get; set; }
    public decimal? BicycleFee { get; set; }
    public decimal? MotorcycleFee { get; set; }
    public decimal? ParkingFee { get; set; }
    
    // Deposits
    public decimal? SecurityDeposit { get; set; }
    public decimal? GasDeposit { get; set; }
    public decimal? RC { get; set; }
    public decimal? AC { get; set; }
    
    // Renewal Information
    public DateOnly? RenewalStartDate { get; set; }
    public DateOnly? RenewalEndDate { get; set; }
    public decimal? RenewalRent { get; set; }
    public decimal? RenewalManagementFee { get; set; }
    public decimal? RenewalParkingFee { get; set; }
    
    // Guarantee Company
    public int? GuaranteeCompanyNo { get; set; }
    public string? GuaranteeCompanyName { get; set; }
    public string? GuaranteeCompanyId { get; set; }
    
    // Additional Information
    public string? Remarks1 { get; set; }
    public string? Remarks2 { get; set; }
    public string? Pet { get; set; }
    public string? WelfareRecipient { get; set; }
    public string? CorporateContract { get; set; }
    public bool? FixedTermLease { get; set; }
    
    // Contractor Details
    public string? ContractorKana { get; set; }
    public string? ContractorPostalCode { get; set; }
    public string? ContractorAddress { get; set; }
    public string? ContractorPhoneNumber { get; set; }
    public string? ContractorMobileNumber { get; set; }
    public string? ContractorEmail { get; set; }
    public string? ContractorGender { get; set; }
    public DateOnly? ContractorBirthDate { get; set; }
    public string? ContractorEmployer { get; set; }
    public string? ContractorEmployerPhone { get; set; }
    
    // Guarantor Details
    public string? GuarantorRelationship { get; set; }
    public string? GuarantorPostalCode { get; set; }
    public string? GuarantorAddress { get; set; }
    public string? GuarantorPhoneNumber { get; set; }
    public string? GuarantorEmail { get; set; }
    public string? GuarantorEmployer { get; set; }
    public string? GuarantorEmployerPhone { get; set; }
    
    // Emergency Contact Details
    public string? EmergencyContactRelationship { get; set; }
    public string? EmergencyContactPostalCode { get; set; }
    public string? EmergencyContactAddress { get; set; }
    public string? EmergencyContactPhoneNumber { get; set; }
    
    // Metadata
    public DateTime? OutputDateTime { get; set; }

    public int ProcessedFileId { get; set; }
    public ProcessedFile? ProcessedFile { get; set; }
}