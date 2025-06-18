namespace CorchEdges.Data.Abstractions;

/// <summary>
/// Defines methods for retrieving metadata about database entities, such as tables and columns.
/// </summary>
public interface IEntityMetadataProvider
{
    /// <summary>
    /// Retrieves the data type of a specified column in a specified table.
    /// </summary>
    /// <param name="entityName">The name of the entity containing the column.</param>
    /// <param name="propertyName">The name of the property whose type is being retrieved.</param>
    /// <returns>The <see cref="Type"/> representing the column's data type.</returns>
    Type GetPropertyType(string entityName, string propertyName);

    /// <summary>
    /// Determines whether the specified entity exists within the metadata provider.
    /// </summary>
    /// <param name="entityName">The name of the entity to check for existence.</param>
    /// <returns>true if the entity exists; otherwise, false.</returns>
    bool HasEntity(string entityName);

    /// Determines whether a specified property exists within a specified entity.
    /// <param name="entityName">
    /// The name of the entity to check for the existence of the column.
    /// </param>
    /// <param name="propertyName">
    /// The name of the property to check for in the specified table.
    /// </param>
    /// <returns>
    /// True if the specified property exists in the entity; otherwise, false.
    /// </returns>
    bool HasProperty(string entityName, string propertyName);

    /// Extracts column mappings from a configuration class associated with the specified entity type.
    /// <param name="entityType">The CLR type of the entity for which column mappings are to be extracted.</param>
    /// <returns>A dictionary containing column mappings, where the key represents the original column name, and the value represents the mapped column name. Returns an empty dictionary if no configuration class is found or an error occurs during processing.
    Dictionary<string, string> ExtractColumnMappingsFromConfiguration(Type entityType);


    IEntityTypeMetaInfo? GetEntityTypeMetaInfo(Type entityType);

    Dictionary<TKey, TResult> MapMetadata<TKey, TResult>(
        Dictionary<TKey, Type> sourceMap,
        Func<TKey, IEntityTypeMetaInfo, (TKey, TResult)> selector) where TKey : notnull;

    Dictionary<string, Dictionary<string, string>> GetColumnMappings();
    Dictionary<string, Type> GetDefaultEntityMappings();
    Dictionary<string, string> GetTableMappings();
}