using System.Reflection;
using CorchEdges.Data.Abstractions;

namespace CorchEdges.Data.Providers;

/// <summary>
/// Provides metadata for entities by leveraging reflection to analyze and retrieve
/// type information about entities and their properties.
/// </summary>
public class ReflectionEntityMetadataProvider : IEntityMetadataProvider
{
    /// <summary>
    /// Stores a mapping of table names to their corresponding entity types.
    /// This dictionary is used to retrieve metadata about entities, such as their properties and data types,
    /// enabling reflection-based operations for column and table metadata.
    /// </summary>
    private readonly Dictionary<string, Type> _entityTypes;

    /// Provides methods for retrieving entity metadata such as table and column mappings using reflection.
    /// This implementation uses a dictionary of entity type mappings where the key is the table name
    /// and the value is the corresponding CLR type.
    public ReflectionEntityMetadataProvider(Dictionary<string, Type> entityTypes)
    {
        _entityTypes = entityTypes ?? throw new ArgumentNullException(nameof(entityTypes));
    }

    /// Retrieves the type of a specified column in a given table.
    /// <param name="tableName">
    /// The name of the table containing the column. This value must not be null or empty.
    /// </param>
    /// <param name="columnName">
    /// The name of the column whose type is to be retrieved. This value must not be null or empty.
    /// </param>
    /// <returns>
    /// The data type of the specified column as a <see cref="Type"/>. This includes nullable wrappers if applicable.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the specified table name does not exist in the entity metadata mapping.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the specified column name is not found in the table's corresponding entity, or when column names do not
    /// match the entity's property names exactly.
    /// </exception>
    public Type GetColumnType(string tableName, string columnName)
    {
        if (string.IsNullOrEmpty(tableName) || !_entityTypes.TryGetValue(tableName, out var entityType))
            throw new ArgumentException($"No entity mapping found for table '{tableName}'");

        // Use same binding flags as original
        var property = entityType.GetProperty(columnName?.Trim() ?? string.Empty, BindingFlags.Public | BindingFlags.Instance);
        if (property == null)
        {
            var availableProperties = string.Join(", ", entityType.GetProperties().Select(p => p.Name));
            throw new InvalidOperationException(
                $"Column '{columnName}' not found in entity '{entityType.Name}' for table '{tableName}'. " +
                $"Column names must match entity property names exactly. " +
                $"Available properties: {availableProperties}");
        }

        // CRITICAL: Return the ACTUAL property type (including nullable wrappers)
        // This matches the original behavior exactly
        return property.PropertyType;
    }

    /// Determines whether a table with the specified name exists in the provided metadata.
    /// <param name="tableName">
    /// The name of the table to check for existence. This value should not be null or empty.
    /// </param>
    /// <returns>
    /// Returns true if the table exists in the metadata; otherwise, false.
    /// </returns>
    public bool HasTable(string tableName) => !string.IsNullOrEmpty(tableName) && _entityTypes.ContainsKey(tableName);

    /// Determines whether the specified table contains a column with the given name.
    /// <param name="tableName">The name of the table to check for the column.</param>
    /// <param name="columnName">The name of the column to look for in the specified table.</param>
    /// <returns>True if the table contains the specified column; otherwise, false.</returns>
    public bool HasColumn(string tableName, string columnName)
    {
        if (string.IsNullOrEmpty(tableName) || string.IsNullOrEmpty(columnName) || !_entityTypes.TryGetValue(tableName, out var entityType))
            return false;
        
        return entityType.GetProperty(columnName, BindingFlags.Public | BindingFlags.Instance) != null;
    }
}