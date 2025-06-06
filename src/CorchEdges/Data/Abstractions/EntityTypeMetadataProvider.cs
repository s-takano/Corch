namespace CorchEdges.Data.Abstractions;

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

public interface IEntityTypeMetaInfo
{
    /// <summary>
    /// Gets the table name for this entity type.
    /// </summary>
    string GetTableName();
    
    /// <summary>
    /// Gets the schema name for this entity type.
    /// </summary>
    string? GetSchemaName();
    
    /// <summary>
    /// Gets comprehensive column metadata for this entity type.
    /// </summary>
    IEnumerable<ColumnMetaInfo> GetColumnMetadata();
    
    /// <summary>
    /// Gets the column mappings for this entity type.
    /// Key: Database column name, Value: Property name
    /// </summary>
    Dictionary<string, string> GetColumnMappings() => 
        GetColumnMetadata().ToDictionary(c => c.ColumnName, c => c.PropertyName);
}