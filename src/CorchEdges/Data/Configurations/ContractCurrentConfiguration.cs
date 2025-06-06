using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CorchEdges.Data.Entities;
using CorchEdges.Data.Configurations;
using CorchEdges.Data.Abstractions;

namespace CorchEdges.Data.Configurations;

public class ContractCurrentConfiguration : BaseEntityConfiguration<ContractCurrent>
{
    public override string GetTableName() => "contract_current";
    
    public override string? GetSchemaName() => "corch_edges_raw";
    
    public override IEnumerable<ColumnMetaInfo> GetColumnMetadata()
    {
        return new[]
        {
            new ColumnMetaInfo(nameof(ContractCurrent.Id), "id", "bigint", true, true, true),
            new ColumnMetaInfo(nameof(ContractCurrent.ContractId), "契約ID", "text", false),
            new ColumnMetaInfo(nameof(ContractCurrent.ResidentCode), "入居者コ－ド", "text", false),
            new ColumnMetaInfo(nameof(ContractCurrent.ContractTypeName), "契約分類名", "text", false),
            new ColumnMetaInfo(nameof(ContractCurrent.PropertyName), "物件名", "text", false),
            new ColumnMetaInfo(nameof(ContractCurrent.RoomType), "部屋分類", "text", false),
            new ColumnMetaInfo(nameof(ContractCurrent.PropertyNo), "物件No", "integer", false),
            new ColumnMetaInfo(nameof(ContractCurrent.RoomNo), "部屋No", "integer", false),
            new ColumnMetaInfo(nameof(ContractCurrent.ContractorNo), "契約者1No", "integer", false),
            new ColumnMetaInfo(nameof(ContractCurrent.ContractorName), "契約者_名", "text", false),
            new ColumnMetaInfo(nameof(ContractCurrent.ResidentName), "入居者1_名", "text", false),
            new ColumnMetaInfo(nameof(ContractCurrent.GuarantorName), "連帯保証人1_名", "text", false),
            new ColumnMetaInfo(nameof(ContractCurrent.EmergencyContactName), "緊急連絡先_名", "text", false),
            new ColumnMetaInfo(nameof(ContractCurrent.ContractStatus), "契約の状態", "text", false),
            new ColumnMetaInfo(nameof(ContractCurrent.CancellationDate), "解約日", "date", false),
            new ColumnMetaInfo(nameof(ContractCurrent.ContractRevisionDate), "契約改定日", "date", false),
            new ColumnMetaInfo(nameof(ContractCurrent.MoveInDate), "入居日(新規契約開始日)", "date", false),
            new ColumnMetaInfo(nameof(ContractCurrent.ContractStartDate), "契約開始日", "date", false),
            new ColumnMetaInfo(nameof(ContractCurrent.DueDate), "期日", "date", false),
            new ColumnMetaInfo(nameof(ContractCurrent.NoticeInDays), "予告", "integer", false),
            new ColumnMetaInfo(nameof(ContractCurrent.Rent), "家賃", "numeric(12,0)", false),
            new ColumnMetaInfo(nameof(ContractCurrent.ManagementFee), "管理費", "numeric(12,0)", false),
            new ColumnMetaInfo(nameof(ContractCurrent.WaterFee), "水道", "numeric(12,0)", false),
            new ColumnMetaInfo(nameof(ContractCurrent.HotWaterFee), "給湯", "numeric(12,0)", false),
            new ColumnMetaInfo(nameof(ContractCurrent.ElectricityFee), "電気", "numeric(12,0)", false),
            new ColumnMetaInfo(nameof(ContractCurrent.PhoneFee), "電話", "numeric(12,0)", false),
            new ColumnMetaInfo(nameof(ContractCurrent.BicycleFee), "自転車", "numeric(12,0)", false),
            new ColumnMetaInfo(nameof(ContractCurrent.MotorcycleFee), "バイク", "numeric(12,0)", false),
            new ColumnMetaInfo(nameof(ContractCurrent.ParkingFee), "駐車料", "numeric(12,0)", false),
            new ColumnMetaInfo(nameof(ContractCurrent.SecurityDeposit), "敷金(家)", "numeric(12,0)", false),
            new ColumnMetaInfo(nameof(ContractCurrent.GasDeposit), "ガス保証金", "numeric(12,0)", false),
            new ColumnMetaInfo(nameof(ContractCurrent.RC), "RC", "numeric(12,0)", false),
            new ColumnMetaInfo(nameof(ContractCurrent.AC), "AC", "numeric(12,0)", false),
            new ColumnMetaInfo(nameof(ContractCurrent.RenewalStartDate), "更新契約始期", "date", false),
            new ColumnMetaInfo(nameof(ContractCurrent.RenewalEndDate), "更新契約終期", "date", false),
            new ColumnMetaInfo(nameof(ContractCurrent.RenewalRent), "更新後家賃", "numeric(12,0)", false),
            new ColumnMetaInfo(nameof(ContractCurrent.RenewalManagementFee), "更新後管理費", "numeric(12,0)", false),
            new ColumnMetaInfo(nameof(ContractCurrent.RenewalWaterFee), "更新後水道", "numeric(12,0)", false),
            new ColumnMetaInfo(nameof(ContractCurrent.RenewalHotWaterFee), "更新後給湯", "numeric(12,0)", false),
            new ColumnMetaInfo(nameof(ContractCurrent.RenewalElectricityFee), "更新後電気", "numeric(12,0)", false),
            new ColumnMetaInfo(nameof(ContractCurrent.RenewalBicycleFee), "更新後自転車", "numeric(12,0)", false),
            new ColumnMetaInfo(nameof(ContractCurrent.RenewalMotorcycleFee), "更新後バイク", "numeric(12,0)", false),
            new ColumnMetaInfo(nameof(ContractCurrent.RenewalPhoneFee), "更新後電話", "numeric(12,0)", false),
            new ColumnMetaInfo(nameof(ContractCurrent.RenewalParkingFee), "更新後駐車料", "numeric(12,0)", false),
            new ColumnMetaInfo(nameof(ContractCurrent.GuaranteeCompanyNo), "保証会社No", "integer", false),
            new ColumnMetaInfo(nameof(ContractCurrent.GuaranteeCompanyName), "保証会社名", "text", false),
            new ColumnMetaInfo(nameof(ContractCurrent.GuaranteeCompanyId), "保証会社ID", "text", false),
            new ColumnMetaInfo(nameof(ContractCurrent.Remarks1), "備考1", "text", false),
            new ColumnMetaInfo(nameof(ContractCurrent.Remarks2), "備考2", "text", false),
            new ColumnMetaInfo(nameof(ContractCurrent.Pet), "ペット", "text", false),
            new ColumnMetaInfo(nameof(ContractCurrent.WelfareRecipient), "生活保護", "text", false),
            new ColumnMetaInfo(nameof(ContractCurrent.CorporateContract), "法人契約", "text", false),
            new ColumnMetaInfo(nameof(ContractCurrent.FixedTermLease), "定期借家", "boolean", false),
            new ColumnMetaInfo(nameof(ContractCurrent.ContractorKana), "契約者_カナ", "text", false),
            new ColumnMetaInfo(nameof(ContractCurrent.ContractorPostalCode), "契約者_郵便番号", "text", false),
            new ColumnMetaInfo(nameof(ContractCurrent.ContractorAddress), "契約者_住所", "text", false),
            new ColumnMetaInfo(nameof(ContractCurrent.ContractorPhoneNumber), "契約者_電話番号", "text", false),
            new ColumnMetaInfo(nameof(ContractCurrent.ContractorMobileNumber), "契約者_携帯電話番号", "text", false),
            new ColumnMetaInfo(nameof(ContractCurrent.ContractorEmail), "契約者_メールアドレス", "text", false),
            new ColumnMetaInfo(nameof(ContractCurrent.ContractorGender), "契約者_性別", "text", false),
            new ColumnMetaInfo(nameof(ContractCurrent.ContractorBirthDate), "契約者_生年月日", "date", false),
            new ColumnMetaInfo(nameof(ContractCurrent.ContractorEmployer), "契約者_勤務先名", "text", false),
            new ColumnMetaInfo(nameof(ContractCurrent.ContractorEmployerPhone), "契約者_勤務先電話番号", "text", false),
            new ColumnMetaInfo(nameof(ContractCurrent.GuarantorRelationship), "連帯保証人1_続柄", "text", false),
            new ColumnMetaInfo(nameof(ContractCurrent.GuarantorPostalCode), "連帯保証人1_郵便番号", "text", false),
            new ColumnMetaInfo(nameof(ContractCurrent.GuarantorAddress), "連帯保証人1_住所", "text", false),
            new ColumnMetaInfo(nameof(ContractCurrent.GuarantorPhoneNumber), "連帯保証人1_電話番号", "text", false),
            new ColumnMetaInfo(nameof(ContractCurrent.GuarantorEmail), "連帯保証人1_メールアドレス", "text", false),
            new ColumnMetaInfo(nameof(ContractCurrent.GuarantorEmployer), "連帯保証人1_勤務先名", "text", false),
            new ColumnMetaInfo(nameof(ContractCurrent.GuarantorEmployerPhone), "連帯保証人1_勤務先電話番号", "text", false),
            new ColumnMetaInfo(nameof(ContractCurrent.EmergencyContactRelationship), "緊急連絡先_続柄", "text", false),
            new ColumnMetaInfo(nameof(ContractCurrent.EmergencyContactPostalCode), "緊急連絡先_郵便番号", "text", false),
            new ColumnMetaInfo(nameof(ContractCurrent.EmergencyContactAddress), "緊急連絡先_住所", "text", false),
            new ColumnMetaInfo(nameof(ContractCurrent.EmergencyContactPhoneNumber), "緊急連絡先_電話番号", "text", false),
            new ColumnMetaInfo(nameof(ContractCurrent.OutputDateTime), "出力日時", "timestamp without time zone", false)
        };
    }
}