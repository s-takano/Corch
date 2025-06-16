using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CorchEdges.Data.Entities;
using CorchEdges.Data.Configurations;
using CorchEdges.Data.Abstractions;

namespace CorchEdges.Data.Configurations;

public class ProcessedFileConfiguration : BaseEntityConfiguration<ProcessedFile>
{
    public override string GetTableName() => "processed_file";

    public override string? GetSchemaName() => "corch_edges_raw";

    public override IEnumerable<ColumnMetaInfo> GetColumnMetadata()
    {
        return new[]
        {
            new ColumnMetaInfo(nameof(ProcessedFile.Id), "Id", "bigint", true, true, true),
            new ColumnMetaInfo(nameof(ProcessedFile.FileName), "FileName", "varchar(500)", false, false, false),
            new ColumnMetaInfo(nameof(ProcessedFile.SharePointItemId), "SharePointItemId", "varchar(100)", false),
            new ColumnMetaInfo(nameof(ProcessedFile.ProcessedAt), "ProcessedAt", "timestamp", false, false, false),
            new ColumnMetaInfo(nameof(ProcessedFile.Status), "Status", "varchar(50)", false, false, false),
            new ColumnMetaInfo(nameof(ProcessedFile.ErrorMessage), "ErrorMessage", "text", false),
            new ColumnMetaInfo(nameof(ProcessedFile.RecordCount), "RecordCount", "integer", false)
        };
    }
}