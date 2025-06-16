using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CorchEdges.Data.Entities;
using CorchEdges.Data.Configurations;
using CorchEdges.Data.Abstractions;

namespace CorchEdges.Data.Configurations;

public class ProcessingLogConfiguration : BaseEntityConfiguration<ProcessingLog>
{
    public override string GetTableName() => "processing_log";

    public override string? GetSchemaName() => "corch_edges_raw";

    public override IEnumerable<ColumnMetaInfo> GetColumnMetadata()
    {
        return new[]
        {
            new ColumnMetaInfo(
                PropertyName: nameof(ProcessingLog.Id),
                ColumnName: "id",
                PostgreSqlType: "bigint",
                IsRequired: true,
                IsKey: true,
                UseIdentityColumn: true
            ),
            new ColumnMetaInfo(
                PropertyName: nameof(ProcessingLog.SiteId),
                ColumnName: "site_id",
                PostgreSqlType: "varchar(50)",
                IsRequired: true,
                MaxLength: 50,
                HasIndex: true
            ),
            new ColumnMetaInfo(
                PropertyName: nameof(ProcessingLog.ListId),
                ColumnName: "list_id",
                PostgreSqlType: "varchar(50)",
                IsRequired: true,
                MaxLength: 50,
                HasIndex: true
            ),
            new ColumnMetaInfo(
                PropertyName: nameof(ProcessingLog.DeltaLink),
                ColumnName: "delta_link",
                PostgreSqlType: "text",
                IsRequired: false
            ),
            new ColumnMetaInfo(
                PropertyName: nameof(ProcessingLog.LastProcessedAt),
                ColumnName: "last_processed_at",
                PostgreSqlType: "timestamp",
                IsRequired: true,
                HasIndex: true
            ),
            new ColumnMetaInfo(
                PropertyName: nameof(ProcessingLog.CreatedAt),
                ColumnName: "created_at",
                PostgreSqlType: "timestamp",
                IsRequired: true
            ),
            new ColumnMetaInfo(
                PropertyName: nameof(ProcessingLog.UpdatedAt),
                ColumnName: "updated_at",
                PostgreSqlType: "timestamp",
                IsRequired: true
            ),
            new ColumnMetaInfo(
                PropertyName: nameof(ProcessingLog.LastProcessedCount),
                ColumnName: "last_processed_count",
                PostgreSqlType: "integer",
                IsRequired: true
            ),
            new ColumnMetaInfo(
                PropertyName: nameof(ProcessingLog.Status),
                ColumnName: "status",
                PostgreSqlType: "varchar(20)",
                IsRequired: true,
                MaxLength: 20,
                HasIndex: true
            ),
            new ColumnMetaInfo(
                PropertyName: nameof(ProcessingLog.LastError),
                ColumnName: "last_error",
                PostgreSqlType: "varchar(1000)",
                IsRequired: false,
                MaxLength: 1000
            ),
            new ColumnMetaInfo(
                PropertyName: nameof(ProcessingLog.SubscriptionId),
                ColumnName: "subscription_id",
                PostgreSqlType: "varchar(100)",
                IsRequired: false,
                MaxLength: 100,
                HasIndex: true
            ),
            new ColumnMetaInfo(
                PropertyName: nameof(ProcessingLog.SuccessfulRuns),
                ColumnName: "successful_runs",
                PostgreSqlType: "integer",
                IsRequired: true
            ),
            new ColumnMetaInfo(
                PropertyName: nameof(ProcessingLog.FailedRuns),
                ColumnName: "failed_runs",
                PostgreSqlType: "integer",
                IsRequired: true
            )
        };
    }
}