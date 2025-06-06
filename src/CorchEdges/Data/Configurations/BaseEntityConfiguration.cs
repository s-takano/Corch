using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CorchEdges.Data.Abstractions;

namespace CorchEdges.Data.Configurations;

public abstract class BaseEntityConfiguration<TEntity> : IEntityTypeConfiguration<TEntity>, IEntityTypeMetaInfo
    where TEntity : class
{
    public abstract string GetTableName();
    public abstract string? GetSchemaName();
    public abstract IEnumerable<ColumnMetaInfo> GetColumnMetadata();

    public virtual void Configure(EntityTypeBuilder<TEntity> builder)
    {
        // Configure table
        if (GetSchemaName() != null)
        {
            builder.ToTable(GetTableName(), GetSchemaName());
        }
        else
        {
            builder.ToTable(GetTableName());
        }

        var columnMetadata = GetColumnMetadata().ToList();
        
        // Configure keys
        var keyColumns = columnMetadata.Where(c => c.IsKey).ToList();
        if (keyColumns.Count == 1)
        {
            var keyColumn = keyColumns.First();
            ConfigureKey(builder, keyColumn.PropertyName);
        }
        else if (keyColumns.Count > 1)
        {
            // Composite key - use property names
            var propertyNames = keyColumns.Select(c => c.PropertyName).ToArray();
            builder.HasKey(propertyNames);
        }

        // Configure properties
        foreach (var column in columnMetadata)
        {
            ConfigureProperty(builder, column);
        }

        // Configure indexes
        var indexColumns = columnMetadata.Where(c => c.HasIndex && !c.IsKey).ToList();
        foreach (var indexColumn in indexColumns)
        {
            ConfigureIndex(builder, indexColumn.PropertyName);
        }
    }

    private void ConfigureKey(EntityTypeBuilder<TEntity> builder, string propertyName)
    {
        var lambda = CreatePropertyExpression(propertyName);
        builder.HasKey(lambda);
    }

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

    private void ConfigureIndex(EntityTypeBuilder<TEntity> builder, string propertyName)
    {
        var lambda = CreatePropertyExpression(propertyName);
        builder.HasIndex(lambda);
    }

    private Expression<Func<TEntity, object?>> CreatePropertyExpression(string propertyName)
    {
        // Use reflection to get the property info
        var propertyInfo = typeof(TEntity).GetProperty(propertyName);
        if (propertyInfo == null)
        {
            throw new InvalidOperationException($"Property '{propertyName}' not found on entity '{typeof(TEntity).Name}'");
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