using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CorchEdges.Data.Entities;

namespace CorchEdges.Data.Configurations;

public class ProcessingLogConfiguration : IEntityTypeConfiguration<ProcessingLog>
{
    public void Configure(EntityTypeBuilder<ProcessingLog> builder)
    {
        // Table configuration
        builder.ToTable("processing_log", "corch_edges_raw");
        
        // Primary key
        builder.HasKey(e => e.Id);
        
        // Properties configuration
        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasColumnType("integer")
            .ValueGeneratedOnAdd()
            .IsRequired();
            
        builder.Property(e => e.SiteId)
            .HasColumnName("site_id")
            .HasColumnType("varchar(150)")
            .HasMaxLength(150)
            .IsRequired();
            
        builder.Property(e => e.ListId)
            .HasColumnName("list_id")
            .HasColumnType("varchar(150)")
            .HasMaxLength(150)
            .IsRequired();
            
        builder.Property(e => e.DeltaLink)
            .HasColumnName("delta_link")
            .HasColumnType("text");
            
        builder.Property(e => e.LastProcessedAt)
            .HasColumnName("last_processed_at")
            .HasColumnType("timestamp without time zone")
            .IsRequired();
            
        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp without time zone")
            .IsRequired();
            
        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp without time zone")
            .IsRequired();
            
        builder.Property(e => e.LastProcessedCount)
            .HasColumnName("last_processed_count")
            .HasColumnType("integer")
            .IsRequired();
            
        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasColumnType("varchar(20)")
            .HasMaxLength(20)
            .IsRequired();
            
        builder.Property(e => e.LastError)
            .HasColumnName("last_error")
            .HasColumnType("varchar(1000)")
            .HasMaxLength(1000);
            
        builder.Property(e => e.SubscriptionId)
            .HasColumnName("subscription_id")
            .HasColumnType("varchar(100)")
            .HasMaxLength(100);
            
        builder.Property(e => e.SuccessfulItems)
            .HasColumnName("successful_runs")
            .HasColumnType("integer")
            .IsRequired();
            
        builder.Property(e => e.FailedItems)
            .HasColumnName("failed_runs")
            .HasColumnType("integer")
            .IsRequired();
        
        // Indexes
        builder.HasIndex(e => e.SiteId)
            .HasDatabaseName("IX_processing_log_site_id");
            
        builder.HasIndex(e => e.ListId)
            .HasDatabaseName("IX_processing_log_list_id");
            
        builder.HasIndex(e => e.LastProcessedAt)
            .HasDatabaseName("IX_processing_log_last_processed_at");
            
        builder.HasIndex(e => e.Status)
            .HasDatabaseName("IX_processing_log_status");
            
        builder.HasIndex(e => e.SubscriptionId)
            .HasDatabaseName("IX_processing_log_subscription_id");
    }
}