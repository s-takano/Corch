using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CorchEdges.Data.Entities;
using CorchEdges.Data.Configurations;
using CorchEdges.Data.Abstractions;

namespace CorchEdges.Data.Configurations;

public class ContractRenewalConfiguration : BaseEntityConfiguration<ContractRenewal>
{
    public override string GetTableName() => "contract_renewal";

    public override string? GetSchemaName() => "corch_edges_raw";

    public override IEnumerable<ColumnMetaInfo> GetColumnMetadata()
    {
        return new[]
        {
            new ColumnMetaInfo(nameof(ContractRenewal.Id), "id", "integer", true, true, true),
            new ColumnMetaInfo(nameof(ContractRenewal.ContractId), "契約ID", "text", false),
            new ColumnMetaInfo(nameof(ContractRenewal.PropertyNo), "物件No", "integer", false),
            new ColumnMetaInfo(nameof(ContractRenewal.RoomNo), "部屋No", "integer", false),
            new ColumnMetaInfo(nameof(ContractRenewal.ContractorNo), "契約者1No", "integer", false),
            new ColumnMetaInfo(nameof(ContractRenewal.PropertyName), "物件名", "text", false),
            new ColumnMetaInfo(nameof(ContractRenewal.ContractorName), "契約者_名", "text", false),
            new ColumnMetaInfo(nameof(ContractRenewal.ProgressStatus), "進捗管理ステータス", "text", false),
            new ColumnMetaInfo(nameof(ContractRenewal.RenewalDate), "更新日", "date", false),
            new ColumnMetaInfo(nameof(ContractRenewal.PreviousContractStartDate), "前契約始期", "date", false),
            new ColumnMetaInfo(nameof(ContractRenewal.PreviousContractEndDate), "前契約終期", "date", false),
            new ColumnMetaInfo(nameof(ContractRenewal.NextContractStartDate), "次契約始期", "date", false),
            new ColumnMetaInfo(nameof(ContractRenewal.NextContractEndDate), "次契約終期", "date", false),
            new ColumnMetaInfo(nameof(ContractRenewal.ContractRenewalDate), "契約更新日", "date", false),
            new ColumnMetaInfo(nameof(ContractRenewal.OutputDateTime), "出力日時", "timestamp without time zone", false)
        };
    }
}