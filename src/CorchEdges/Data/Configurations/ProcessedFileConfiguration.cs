using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CorchEdges.Data.Entities;

namespace CorchEdges.Data.Configurations;

public class ProcessedFileConfiguration : IEntityTypeConfiguration<ProcessedFile>
{
    public void Configure(EntityTypeBuilder<ProcessedFile> builder)
    {
        // Table configuration
        builder.ToTable("processed_file", "corch_edges_raw");
        
        // Primary key
        builder.HasKey(e => e.Id);
        
        // Properties configuration
        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasColumnType("integer")
            .ValueGeneratedOnAdd()
            .IsRequired();
            
        builder.Property(e => e.FileName)
            .HasColumnName("file_name")
            .HasColumnType("varchar(500)")
            .IsRequired();
            
        builder.Property(e => e.SharePointItemId)
            .HasColumnName("share_point_item_id")
            .HasColumnType("varchar(100)");
            
        builder.Property(e => e.ProcessedAt)
            .HasColumnName("processed_at")
            .HasColumnType("timestamp without time zone")
            .IsRequired();
            
        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasColumnType("varchar(50)")
            .IsRequired();
            
        builder.Property(e => e.ErrorMessage)
            .HasColumnName("error_message")
            .HasColumnType("text");
            
        builder.Property(e => e.RecordCount)
            .HasColumnName("record_count")
            .HasColumnType("integer");
    }
}