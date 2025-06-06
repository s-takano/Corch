using System.Reflection;
using CorchEdges.Data.Abstractions;

namespace CorchEdges.Data.Providers;

public class ReflectionEntityMetadataProvider : IEntityMetadataProvider
{
    private readonly Dictionary<string, Type> _entityTypes;

    public ReflectionEntityMetadataProvider(Dictionary<string, Type> entityTypes)
    {
        _entityTypes = entityTypes ?? throw new ArgumentNullException(nameof(entityTypes));
    }

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

    public bool HasTable(string tableName) => !string.IsNullOrEmpty(tableName) && _entityTypes.ContainsKey(tableName);

    public bool HasColumn(string tableName, string columnName)
    {
        if (string.IsNullOrEmpty(tableName) || string.IsNullOrEmpty(columnName) || !_entityTypes.TryGetValue(tableName, out var entityType))
            return false;
        
        return entityType.GetProperty(columnName, BindingFlags.Public | BindingFlags.Instance) != null;
    }
}