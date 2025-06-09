namespace CorchEdges.Data.Abstractions;

/// <summary>
/// Represents metadata information about a database column, including its mapping
/// to a property in an entity model and its database-specific characteristics.
/// </summary>
public record ColumnMetaInfo(
    string PropertyName,
    string ColumnName,
    string? PostgreSqlType = null,
    bool IsRequired = false,
    bool IsKey = false,
    bool UseIdentityColumn = false,
    int? MaxLength = null,
    bool HasIndex = false
);