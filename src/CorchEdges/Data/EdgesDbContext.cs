using Microsoft.EntityFrameworkCore;
using CorchEdges.Data.Entities;

namespace CorchEdges.Data;

public class EdgesDbContext : DbContext
{
    // Add a constructor that accepts options
    public EdgesDbContext(DbContextOptions<EdgesDbContext> options) : base(options)
    {
    }

    // Metadata tables
    public DbSet<ProcessedFile> ProcessedFiles { get; set; }
    public DbSet<ProcessingLog> ProcessingLogs { get; set; }
    
    // Raw data tables
    public DbSet<ContractCreation> ContractCreations { get; set; }
    public DbSet<ContractCurrent> ContractCurrents { get; set; }
    public DbSet<ContractRenewal> ContractRenewals { get; set; }
    public DbSet<ContractTermination> ContractTerminations { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Automatically apply all IEntityTypeConfiguration<T> implementations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EdgesDbContext).Assembly);
    }
}