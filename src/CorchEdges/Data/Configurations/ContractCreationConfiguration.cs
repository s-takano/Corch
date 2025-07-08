using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CorchEdges.Data.Entities;
using CorchEdges.Data.Configurations;
using CorchEdges.Data.Abstractions;

namespace CorchEdges.Data.Configurations;

public class ContractCreationConfiguration : BaseEntityConfiguration<ContractCreation>
{
    public override string SheetName { get; } = "新規to業務管理";
    
    public override string GetTableName() => "contract_creation";

    public override string? GetSchemaName() => "corch_edges_raw";

    public override IEnumerable<ColumnMetaInfo> GetColumnMetadata()
    {
        return
        [
            new ColumnMetaInfo(nameof(ContractCreation.Id), "id", "integer", true, true, true),
            new ColumnMetaInfo(nameof(ContractCreation.ContractId), "契約ID", "text", false),
            new ColumnMetaInfo(nameof(ContractCreation.PropertyNo), "物件No", "integer", false),
            new ColumnMetaInfo(nameof(ContractCreation.RoomNo), "部屋No", "integer", false),
            new ColumnMetaInfo(nameof(ContractCreation.ContractorNo), "契約者1No", "integer", false),
            new ColumnMetaInfo(nameof(ContractCreation.PropertyName), "物件名", "text", false),
            new ColumnMetaInfo(nameof(ContractCreation.ContractorName), "契約者名", "text", false),
            new ColumnMetaInfo(nameof(ContractCreation.ProgressStatus), "進捗管理ステータス", "text", false),
            new ColumnMetaInfo(nameof(ContractCreation.ContractStatus), "契約ステータス", "text", false),
            new ColumnMetaInfo(nameof(ContractCreation.ApplicationDate), "入居申込日", "date", false),
            new ColumnMetaInfo(nameof(ContractCreation.MoveInDate), "入居予定日", "date", false),
            new ColumnMetaInfo(nameof(ContractCreation.KeyHandoverDate), "鍵引渡日", "date", false),
            new ColumnMetaInfo(nameof(ContractCreation.ContractDate), "契約日", "date", false),
            new ColumnMetaInfo(nameof(ContractCreation.KeyMoney), "礼金(家)", "numeric(12,0)", false),
            new ColumnMetaInfo(nameof(ContractCreation.SecurityDeposit), "敷金(家)", "numeric(12,0)", false),
            new ColumnMetaInfo(nameof(ContractCreation.BrokerageFee), "仲介手数料", "numeric(12,0)", false),
            new ColumnMetaInfo(nameof(ContractCreation.GuaranteeFee), "保証料", "numeric(12,0)", false),
            new ColumnMetaInfo(nameof(ContractCreation.ApartmentInsurance), "ｱﾊﾟ-ﾄ保険代", "numeric(12,0)", false),
            new ColumnMetaInfo(nameof(ContractCreation.KeyReplacementFee), "鍵交換費", "numeric(12,0)", false),
            new ColumnMetaInfo(nameof(ContractCreation.DocumentStampFee), "用紙印紙代", "numeric(12,0)", false),
            new ColumnMetaInfo(nameof(ContractCreation.WithdrawalFee), "引落手数料", "numeric(12,0)", false),
            new ColumnMetaInfo(nameof(ContractCreation.BicycleRegistrationFee), "自転車登録事務手数料", "numeric(12,0)", false),
            new ColumnMetaInfo(nameof(ContractCreation.MotorcycleRegistrationFee), "バイク登録事務手数料", "numeric(12,0)",
                false),
            new ColumnMetaInfo(nameof(ContractCreation.InternetApplicationFee), "インターネット申込金", "numeric(12,0)", false),
            new ColumnMetaInfo(nameof(ContractCreation.MaximumAmount), "極度額", "numeric(12,0)", false),
            new ColumnMetaInfo(nameof(ContractCreation.OutputDateTime), "出力日時", "timestamp without time zone", false)
        ];
    }


}