using CorchEdges.Data.Abstractions;

namespace CorchEdges.Data.Mappers;

/// <summary>
/// Provides functionality to map an entity's table name based on pre-defined mappings.
/// </summary>
public class EntityBasedTableMapper : ITableNameMapper
{
    /// <summary>
    /// Stores the mappings of original table names to their corresponding mapped table names.
    /// This dictionary is used to define how table names are translated
    /// based on specific rules or requirements.
    /// </summary>
    private readonly Dictionary<string, string> _mappings;

    /// Provides functionality for mapping original table names to predefined table names using a dictionary-based mapping configuration.
    public EntityBasedTableMapper(Dictionary<string, string> mappings)
    {
        _mappings = mappings ?? throw new ArgumentNullException(nameof(mappings));
    }

    /// Maps the original table name to a mapped table name based on predefined mappings.
    /// If the original table name is invalid or not found in the mappings, an exception is thrown.
    /// <param name="originalTableName">The original table name that needs to be mapped.</param>
    /// <returns>A string representing the mapped table name.</returns>
    /// <exception cref="System.ArgumentException">Thrown when the provided table name is invalid or not mapped.</exception>
    public string MapTableName(string originalTableName)
    {
        if (!string.IsNullOrWhiteSpace(originalTableName) && _mappings.TryGetValue(originalTableName, out var mapped))
            return mapped;
        
        throw new ArgumentException($"Invalid table name: {originalTableName}");
    }
}