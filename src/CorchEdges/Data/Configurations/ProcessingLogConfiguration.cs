using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CorchEdges.Data.Entities;
using CorchEdges.Data.Configurations;
using CorchEdges.Data.Abstractions;

namespace CorchEdges.Data.Configurations;

public class ProcessingLogConfiguration : BaseEntityConfiguration<ProcessingLog>
{
    public override string GetTableName() => "processing_log";

    public override string? GetSchemaName() => null; // Assuming default schema

    public override IEnumerable<ColumnMetaInfo> GetColumnMetadata()
    {
        return new[]
        {
            new ColumnMetaInfo(nameof(ProcessingLog.Id), "Id", "bigint", true, true, true),
            new ColumnMetaInfo(nameof(ProcessingLog.Level), "Level", "varchar(20)", false, false, false),
            new ColumnMetaInfo(nameof(ProcessingLog.Message), "Message", "text", false, false, false),
            new ColumnMetaInfo(nameof(ProcessingLog.CreatedAt), "CreatedAt", "timestamp", false, false, false),
            new ColumnMetaInfo(nameof(ProcessingLog.SharePointItemId), "SharePointItemId", "varchar(100)", false),
            new ColumnMetaInfo(nameof(ProcessingLog.ExceptionDetails), "ExceptionDetails", "text", false)
        };
    }
}