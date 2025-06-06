using CorchEdges.Data.Abstractions;

namespace CorchEdges.Data.Mappers;

public class EntityBasedTableMapper : ITableNameMapper
{
    private readonly Dictionary<string, string> _mappings;

    public EntityBasedTableMapper(Dictionary<string, string> mappings)
    {
        _mappings = mappings ?? throw new ArgumentNullException(nameof(mappings));
    }

    public string MapTableName(string originalTableName)
    {
        if (!string.IsNullOrWhiteSpace(originalTableName) && _mappings.TryGetValue(originalTableName, out var mapped))
            return mapped;
        
        throw new ArgumentException($"Invalid table name: {originalTableName}");
    }
}