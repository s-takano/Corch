using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CorchEdges.Data.Entities;
using CorchEdges.Data.Configurations;
using CorchEdges.Data.Abstractions;

namespace CorchEdges.Data.Configurations;

public class ContractTerminationConfiguration : BaseEntityConfiguration<ContractTermination>
{
    public override string SheetName { get; } = "解約to業務管理";
    public override string GetTableName() => "contract_termination";

    public override string? GetSchemaName() => "corch_edges_raw";

    public override IEnumerable<ColumnMetaInfo> GetColumnMetadata()
    {
        return new[]
        {
            new ColumnMetaInfo(nameof(ContractTermination.Id), "id", "integer", true, true, true),
            new ColumnMetaInfo(nameof(ContractTermination.ContractId), "契約ID", "text", false),
            new ColumnMetaInfo(nameof(ContractTermination.PropertyNo), "物件No", "integer", false),
            new ColumnMetaInfo(nameof(ContractTermination.RoomNo), "部屋No", "integer", false),
            new ColumnMetaInfo(nameof(ContractTermination.ContractorNo), "契約者1No", "integer", false),
            new ColumnMetaInfo(nameof(ContractTermination.PropertyName), "物件名", "text", false),
            new ColumnMetaInfo(nameof(ContractTermination.ContractorName), "契約者_名", "text", false),
            new ColumnMetaInfo(nameof(ContractTermination.RoomType), "部屋分類", "text", false),
            new ColumnMetaInfo(nameof(ContractTermination.ProgressStatus), "進捗管理ステータス", "text", false),
            new ColumnMetaInfo(nameof(ContractTermination.ApplicationDate), "_受付日", "date", false),
            new ColumnMetaInfo(nameof(ContractTermination.NotificationReceiptDate), "届受取日", "date", false),
            new ColumnMetaInfo(nameof(ContractTermination.ScheduledMoveOutDate), "_転出予定日", "date", false),
            new ColumnMetaInfo(nameof(ContractTermination.ChangeMonth), "変更月", "date", false),
            new ColumnMetaInfo(nameof(ContractTermination.ActualMoveOutDate), "_転出日", "date", false),
            new ColumnMetaInfo(nameof(ContractTermination.ProrationDate), "_日割日", "date", false),
            new ColumnMetaInfo(nameof(ContractTermination.MoveOutInspectionDate), "転出点検日", "date", false),
            new ColumnMetaInfo(nameof(ContractTermination.MoveOutInspector), "転出点検者", "text", false),
            new ColumnMetaInfo(nameof(ContractTermination.SettlementCreationDate), "精算書作成日", "date", false),
            new ColumnMetaInfo(nameof(ContractTermination.SettlementCreator), "精算書作成者", "text", false),
            new ColumnMetaInfo(nameof(ContractTermination.MeetingDate), "打合せ日", "date", false),
            new ColumnMetaInfo(nameof(ContractTermination.FinalApprovalDate), "最終決裁日", "date", false),
            new ColumnMetaInfo(nameof(ContractTermination.SettlementSentDate), "精算書送付日", "date", false),
            new ColumnMetaInfo(nameof(ContractTermination.SettlementSender), "精算書送付者", "text", false),
            new ColumnMetaInfo(nameof(ContractTermination.SettlementPaymentDate), "精算金送金日", "date", false),
            new ColumnMetaInfo(nameof(ContractTermination.ReverseChargePaymentDate), "逆請求入金日", "date", false),
            new ColumnMetaInfo(nameof(ContractTermination.DocumentScanDate), "書類スキャン日", "date", false),
            new ColumnMetaInfo(nameof(ContractTermination.ConstructionNumber), "工事No", "text", false),
            new ColumnMetaInfo(nameof(ContractTermination.OutputDateTime), "出力日時", "timestamp without time zone", false)
        };
    }
}