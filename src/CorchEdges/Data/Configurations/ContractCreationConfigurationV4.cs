using CorchEdges.Data.Entities;
using CorchEdges.Data.Abstractions;

namespace CorchEdges.Data.Configurations;

public class ContractCreationConfigurationV4 : BaseEntityConfiguration<ContractCreation>
{
    public override string SheetName { get; } = "新規to業務管理";
    
    public override string GetTableName() => "contract_creation";

    public override string GetSchemaName() => "corch_edges_raw";

    public override IEnumerable<ColumnMetaInfo> GetColumnMetadata()
    {
        return
        [
            new ColumnMetaInfo(nameof(ContractCreation.Id), "id", "integer", true, true, true),
            new ColumnMetaInfo(nameof(ContractCreation.ContractId), "契約ID", "text"),
            new ColumnMetaInfo(nameof(ContractCreation.PropertyNo), "物件No", "integer"),
            new ColumnMetaInfo(nameof(ContractCreation.RoomNo), "部屋No", "integer"),
            new ColumnMetaInfo(nameof(ContractCreation.ContractorNo), "契約者1No", "integer"),
            new ColumnMetaInfo(nameof(ContractCreation.ReferenceId), "新規ID", "integer"),
            new ColumnMetaInfo(nameof(ContractCreation.PropertyName), "物件名", "text"),
            new ColumnMetaInfo(nameof(ContractCreation.ContractorName), "契約者名", "text"),
            new ColumnMetaInfo(nameof(ContractCreation.ProgressStatus), "進捗管理ステータス", "text"),
            new ColumnMetaInfo(nameof(ContractCreation.ContractStatus), "契約ステータス", "text"),
            new ColumnMetaInfo(nameof(ContractCreation.ApplicationDate), "入居申込日", "date"),
            new ColumnMetaInfo(nameof(ContractCreation.MoveInDate), "入居予定日", "date"),
            new ColumnMetaInfo(nameof(ContractCreation.KeyHandoverDate), "鍵引渡日", "date"),
            new ColumnMetaInfo(nameof(ContractCreation.ContractDate), "契約日", "date"),
            new ColumnMetaInfo(nameof(ContractCreation.ContractStartDate), "契約始期", "date"),
            new ColumnMetaInfo(nameof(ContractCreation.ContractEndDate), "契約終期", "date"),
            new ColumnMetaInfo(nameof(ContractCreation.BrokerageCommission), "広告料", "numeric(12,0)"),
            new ColumnMetaInfo(nameof(ContractCreation.LeadSource), "販路", "text"),
            new ColumnMetaInfo(nameof(ContractCreation.LeadSourceDetail), "販路その他", "text"),
            new ColumnMetaInfo(nameof(ContractCreation.AccountManager), "上司報告者", "text"),
            new ColumnMetaInfo(nameof(ContractCreation.SupervisorApprovalDate), "上司確認日", "date"),
            new ColumnMetaInfo(nameof(ContractCreation.KeyMoney), "礼金_家", "numeric(12,0)"),
            new ColumnMetaInfo(nameof(ContractCreation.SecurityDeposit), "敷金_家", "numeric(12,0)"),
            new ColumnMetaInfo(nameof(ContractCreation.BrokerageFee), "仲介手数料", "numeric(12,0)"),
            new ColumnMetaInfo(nameof(ContractCreation.GuaranteeFee), "保証料", "numeric(12,0)"),
            new ColumnMetaInfo(nameof(ContractCreation.ApartmentInsurance), "アパート保険代", "numeric(12,0)"),
            new ColumnMetaInfo(nameof(ContractCreation.KeyReplacementFee), "鍵交換費", "numeric(12,0)"),
            new ColumnMetaInfo(nameof(ContractCreation.DocumentStampFee), "用紙印紙代", "numeric(12,0)"),
            new ColumnMetaInfo(nameof(ContractCreation.WithdrawalFee), "引落手数料", "numeric(12,0)"),
            new ColumnMetaInfo(nameof(ContractCreation.BicycleRegistrationFee), "自転車登録事務手数料", "numeric(12,0)"),
            new ColumnMetaInfo(nameof(ContractCreation.MotorcycleRegistrationFee), "バイク登録事務手数料", "numeric(12,0)"),
            new ColumnMetaInfo(nameof(ContractCreation.InternetApplicationFee), "インターネット申込金", "numeric(12,0)"),
            new ColumnMetaInfo(nameof(ContractCreation.MaximumAmount), "極度額", "numeric(12,0)"),
            new ColumnMetaInfo(nameof(ContractCreation.FixedTermContract), "定期借家", "text"),
            new ColumnMetaInfo(nameof(ContractCreation.OutputDateTime), "出力日時", "timestamp without time zone")
        ];
    }


}