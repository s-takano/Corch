namespace CorchEdges.Data.Abstractions;

/// <summary>
/// Defines metadata retrieval methods for an entity type, including table name, schema name, column metadata,
/// and column mappings. Useful in configurations where detailed entity metadata is required for database mapping.
/// </summary>
public interface IEntityTypeMetaInfo
{
    /// <summary>
    /// Gets the table name for this entity type.
    /// </summary>
    string GetTableName();

    /// <summary>
    /// Gets the schema name for this entity type.
    /// </summary>
    /// <returns>
    /// The schema name as a string, or null if no schema is set for this entity type.
    /// </returns>
    string? GetSchemaName();

    /// <summary>
    /// Gets comprehensive metadata about the columns for this entity type.
    /// </summary>
    /// <returns>
    /// A collection of <see cref="ColumnMetaInfo"/> objects, each containing detailed
    /// information about a column's database and entity model mapping.
    /// </returns>
    IEnumerable<ColumnMetaInfo> GetColumnMetadata();

    /// <summary>
    /// Gets the column mappings for this entity type.
    /// The mappings consist of a dictionary where the key is the column name in the database,
    /// and the value is the corresponding property name in the entity model.
    /// </summary>
    /// <returns>
    /// A dictionary containing column-to-property mappings for the entity type.
    /// </returns>
    Dictionary<string, string> GetColumnMappings() =>
        GetColumnMetadata().ToDictionary(c => c.ColumnName, c => c.PropertyName);
    
    
    Type EntityType { get; }
    
    string SheetName { get; }
}