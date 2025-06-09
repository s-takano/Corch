using Microsoft.EntityFrameworkCore;
using CorchEdges.Data.Entities;

namespace CorchEdges.Data;

/// <summary>
/// Represents the Entity Framework database context for the CorchEdges application.
/// </summary>
/// <remarks>
/// This class derives from DbContext and is used to interact with the database.
/// It defines the DbSet properties required for querying and saving instances of entity types.
/// </remarks>
public class EdgesDbContext : DbContext
{
    // Add a constructor that accepts options
    /// <summary>
    /// The EdgesDbContext class provides an Entity Framework Core database context
    /// for interacting with the tables and entities of the system.
    /// It acts as a bridge between the database and the application code.
    /// </summary>
    /// <remarks>
    /// This class includes DbSet properties for metadata and raw data tables.
    /// The OnModelCreating method is overridden to apply all entity configurations
    /// found in the assembly containing the context.
    /// </remarks>
    public EdgesDbContext(DbContextOptions<EdgesDbContext> options) : base(options)
    {
    }

    // Metadata tables
    /// Represents the DbSet for managing and querying metadata about processed files in the system.
    /// This property is a part of the application's data context, allowing for interaction with
    /// the `ProcessedFile` entity, which contains details such as the file name, processing status,
    /// processing time, any error messages, and the number of records processed.
    /// It is primarily used for maintaining a log of file processing operations and their outcomes.
    /// Example scenarios include:
    /// - Adding new entries for files that are being processed.
    /// - Updating existing records when the processing completes.
    /// - Retrieving operational metadata for reporting or auditing purposes.
    public DbSet<ProcessedFile> ProcessedFiles { get; set; }

    /// Represents the database set for processing logs.
    /// This property is utilized to interact with the `ProcessingLog` entity in the database.
    /// Each `ProcessingLog` captures details of a specific operation or event, such as
    /// logging level, message, timestamps, and optional information about errors or associations with external systems.
    public DbSet<ProcessingLog> ProcessingLogs { get; set; }
    
    // Raw data tables
    /// <summary>
    /// Represents the <see cref="DbSet{TEntity}"/> for the <see cref="ContractCreation"/> entity in the database context.
    /// </summary>
    /// <remarks>
    /// This property enables interaction with the ContractCreation table in the database.
    /// It allows CRUD (Create, Read, Update, Delete) operations and LINQ queries against the table.
    /// </remarks>
    /// <seealso cref="EdgesDbContext"/>
    /// <seealso cref="ContractCreation"/>
    public DbSet<ContractCreation> ContractCreations { get; set; }

    /// <summary>
    /// Represents the DbSet for ContractCurrent entities in the EdgesDbContext, allowing interaction
    /// with the ContractCurrent table in the database. This property facilitates querying, adding,
    /// updating, and deleting ContractCurrent records.
    /// </summary>
    public DbSet<ContractCurrent> ContractCurrents { get; set; }

    /// Represents the collection of `ContractRenewal` entities in the database.
    /// This property is used to query and manage data related to contract renewal records.
    /// Maps to the `ContractRenewal` class and its respective table in the database.
    public DbSet<ContractRenewal> ContractRenewals { get; set; }

    /// Represents the DbSet for managing entities of type ContractTermination in the database context.
    /// This property is used to perform CRUD operations for the ContractTermination entity.
    public DbSet<ContractTermination> ContractTerminations { get; set; }

    /// <summary>
    /// Configures the model for the database context during model creation.
    /// </summary>
    /// <param name="modelBuilder">An instance of <see cref="ModelBuilder"/> used to configure entity mappings and relationships.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Automatically apply all IEntityTypeConfiguration<T> implementations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EdgesDbContext).Assembly);
    }
}