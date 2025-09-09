using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CorchEdges.Data.Abstractions;
using CorchEdges.Data.Entities;

namespace CorchEdges.Data.Configurations;

/// Represents a base configuration class for entities used by Entity Framework Core.
/// This abstract class provides common configurations for entities, such as table and schema names,
/// primary keys, and other property configurations. It utilizes column metadata for defining these properties.
/// Type Parameter:
/// TEntity:
/// The type of the entity being configured. It must be a class.
/// Implemented Interfaces:
/// - IEntityTypeConfiguration<TEntity>: Provides configuration for a specific entity type.
/// - IEntityTypeMetaInfo: Exposes metadata about the entity type.
/// Responsibilities:
/// - Defines the table name and schema for the entity using the `GetTableName` and `GetSchemaName` methods.
/// - Configures primary and composite keys based on column metadata.
/// - Configures entity properties and their attributes using the `ConfigureProperty` method.
/// - Configures indexes on specified columns using the `ConfigureIndex` method.
/// Methods:
/// - abstract string GetTableName():
/// Must be implemented to return the table name for the entity.
/// - abstract string? GetSchemaName():
/// Must be implemented to return the schema name for the entity, if applicable.
/// - abstract IEnumerable<ColumnMetaInfo> GetColumnMetadata():
/// Must be implemented to return metadata for all columns in the entity.
/// - virtual void Configure(EntityTypeBuilder<TEntity> builder):
/// Configures the entity type, including table, schema, keys, properties, and indexes.
/// This method can be overridden for additional customization.
public abstract class BaseEntityConfiguration<TEntity> : IEntityTypeConfiguration<TEntity>, IEntityTypeMetaInfo
    where TEntity : class
{
    /// <summary>
    /// Retrieves the name of the database table associated with the entity.
    /// This method is generally used in entity configuration classes to map an entity to its respective table name.
    /// </summary>
    /// <returns>The name of the database table as a string.</returns>
    public abstract string GetTableName();

    /// <summary>
    /// Retrieves the schema name associated with the entity.
    /// </summary>
    /// <returns>
    /// A string representing the schema name, or null if no schema name is defined.
    /// </returns>
    public abstract string? GetSchemaName();

    /// <summary>
    /// Provides metadata about the columns of the entity, such as property name, column name, data type, and constraints.
    /// This method is typically used for defining entity configurations and customizing mapping between entity properties
    /// and database table columns.
    /// </summary>
    /// <returns>A collection of <see cref="ColumnMetaInfo"/> instances representing metadata for each column of the entity.</returns>
    public abstract IEnumerable<ColumnMetaInfo> GetColumnMetadata();

    public Type EntityType { get; } = typeof(TEntity);

    public abstract string SheetName { get; }

    /// <summary>
    /// Configures the entity type used in the Entity Framework Core model by setting up the table name,
    /// schema, keys, properties, and indexes based on the column metadata provided by the entity type.
    /// </summary>
    /// <param name="builder">The builder being used to configure the entity type.</param>
    public virtual void Configure(EntityTypeBuilder<TEntity> builder)
    {
        // Configure table
        if (GetSchemaName() != null)
            builder.ToTable(GetTableName(), GetSchemaName());
        else
            builder.ToTable(GetTableName());

        // Get all column metadata and add ProcessedFileId configuration for entities that require it
        var columnMetadata = GetColumnMetadata().ToList();
        if (columnMetadata.All(m => m.ColumnName != "ProcessedFileId"))
        {
            columnMetadata.Add(new ColumnMetaInfo
            (
                nameof(ContractCreation.ProcessedFileId),
                "ProcessedFileId",
                "integer",
                true,
                false,
                false,
                null,
                true
            ));
        }

        // Configure keys
        var keyColumns = columnMetadata.Where(c => c.IsKey).ToList();
        switch (keyColumns.Count)
        {
            case 1:
            {
                var keyColumn = keyColumns.First();
                ConfigureKey(builder, keyColumn.PropertyName);
                break;
            }
            case > 1:
            {
                // Composite key - use property names
                var propertyNames = keyColumns.Select(c => c.PropertyName).ToArray();
                builder.HasKey(propertyNames);
                break;
            }
        }

        // Configure properties
        foreach (var column in columnMetadata) ConfigureProperty(builder, column);

        // Configure indexes
        var indexColumns = columnMetadata.Where(c => c.HasIndex && !c.IsKey).ToList();
        foreach (var indexColumn in indexColumns) ConfigureIndex(builder, indexColumn.PropertyName);
    }

    /// <summary>
    /// Configures the primary key for the specified entity type using the provided property name.
    /// </summary>
    /// <param name="builder">
    /// The <see cref="EntityTypeBuilder{TEntity}"/> used to configure the entity type.
    /// </param>
    /// <param name="propertyName">
    /// The name of the property to configure as the primary key.
    /// </param>
    private void ConfigureKey(EntityTypeBuilder<TEntity> builder, string propertyName)
    {
        var lambda = CreatePropertyExpression(propertyName);
        builder.HasKey(lambda);
    }

    /// <summary>
    /// Configures a specific property of the entity using the provided column metadata.
    /// </summary>
    /// <param name="builder">
    /// The <see cref="EntityTypeBuilder{TEntity}"/> used to configure the entity.
    /// </param>
    /// <param name="column">
    /// An instance of <see cref="ColumnMetaInfo"/> that provides metadata information
    /// about the column to be configured.
    /// </param>
    private void ConfigureProperty(EntityTypeBuilder<TEntity> builder, ColumnMetaInfo column)
    {
        var lambda = CreatePropertyExpression(column.PropertyName);
        var propertyBuilder = builder.Property(lambda);

        // Configure column name
        propertyBuilder.HasColumnName(column.ColumnName);

        // Configure PostgreSQL type
        if (!string.IsNullOrEmpty(column.PostgreSqlType))
        {
            propertyBuilder.HasColumnType(column.PostgreSqlType);
        }

        // Configure required
        if (column.IsRequired)
        {
            propertyBuilder.IsRequired();
        }

        // Configure max length
        if (column.MaxLength.HasValue)
        {
            propertyBuilder.HasMaxLength(column.MaxLength.Value);
        }

        // Configure identity column
        if (column.UseIdentityColumn)
        {
            propertyBuilder.UseIdentityColumn();
        }
    }

    /// <summary>
    /// Configures an index for the specified entity property using the provided entity type builder.
    /// </summary>
    /// <param name="builder">The <see cref="EntityTypeBuilder{TEntity}"/> used to configure the entity type.</param>
    /// <param name="propertyName">The name of the property on which the index will be configured.</param>
    private void ConfigureIndex(EntityTypeBuilder<TEntity> builder, string propertyName)
    {
        var lambda = CreatePropertyExpression(propertyName);
        builder.HasIndex(lambda);
    }

    /// Creates a lambda expression for a specified property of an entity type.
    /// The lambda expression can be used to configure properties in Entity Framework Core.
    /// <param name="propertyName">
    /// The name of the property for which the expression should be created.
    /// </param>
    /// <returns>
    /// A lambda expression of type <see cref="Expression{Func{TEntity, Object}}"/> representing the specified property.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the specified property name does not exist in the entity type.
    /// </exception>
    private Expression<Func<TEntity, object?>> CreatePropertyExpression(string propertyName)
    {
        // Use reflection to get the property info
        var propertyInfo = typeof(TEntity).GetProperty(propertyName);
        if (propertyInfo == null)
        {
            throw new InvalidOperationException(
                $"Property '{propertyName}' not found on entity '{typeof(TEntity).Name}'");
        }

        // Create the lambda expression: e => e.PropertyName
        var parameter = Expression.Parameter(typeof(TEntity), "e");
        var property = Expression.Property(parameter, propertyInfo);

        // Convert to nullable object to match EF Core's expected signature
        Expression converted;
        if (property.Type == typeof(object))
        {
            converted = property;
        }
        else
        {
            converted = Expression.Convert(property, typeof(object));
        }

        return Expression.Lambda<Func<TEntity, object?>>(converted, parameter);
    }
}