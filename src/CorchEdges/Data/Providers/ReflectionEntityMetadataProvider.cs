using System.Reflection;
using CorchEdges.Data.Abstractions;
using CorchEdges.Data.Entities;
using Microsoft.EntityFrameworkCore;
namespace CorchEdges.Data.Providers;


/// <summary>
/// Provides metadata for entities by leveraging reflection to analyze and retrieve
/// type information about entities and their properties.
/// </summary>
public class ReflectionEntityMetadataProvider : IEntityMetadataProvider
{

    private static readonly Dictionary<string, Type> Entities =
        new()
        {
            { "corch_edges_raw.contract_creation", typeof(ContractCreation) },
            { "corch_edges_raw.contract_current", typeof(ContractCurrent) },
            { "corch_edges_raw.contract_renewal", typeof(ContractRenewal) },
            { "corch_edges_raw.contract_termination", typeof(ContractTermination) },
        };

    private Dictionary<string, Type> SheetEntityMap
        => MapMetadata(
            Entities,
            (sheetName, metadata) => (metadata.SheetName, metadata.EntityType));
    
    

    public ReflectionEntityMetadataProvider()
    {
    }

    /// Retrieves the type of a specified column in a given table.
    /// <param name="entityName">
    /// The name of the entityName. This value must not be null or empty.
    /// </param>
    /// <param name="propertyName">
    /// The name of the property whose type is to be retrieved. This value must not be null or empty.
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
    public Type GetPropertyType(string entityName, string propertyName)
    {
        if (string.IsNullOrEmpty(entityName) || !Entities.TryGetValue(entityName, out var entityType))
            throw new ArgumentException($"No entity mapping found for table '{entityName}'");

        // Use same binding flags as original
        var property = entityType.GetProperty(propertyName?.Trim() ?? string.Empty, BindingFlags.Public | BindingFlags.Instance);
        if (property == null)
        {
            var availableProperties = string.Join(", ", entityType.GetProperties().Select(p => p.Name));
            throw new InvalidOperationException(
                $"Column '{propertyName}' not found in entity '{entityType.Name}' for table '{entityName}'. " +
                $"Column names must match entity property names exactly. " +
                $"Available properties: {availableProperties}");
        }

        // CRITICAL: Return the ACTUAL property type (including nullable wrappers)
        // This matches the original behavior exactly
        return property.PropertyType;
    }

    /// Determines whether a table with the specified name exists in the provided metadata.
    /// <param name="entityName">
    /// The name of the entity to check for existence. This value should not be null or empty.
    /// </param>
    /// <returns>
    /// Returns true if the entity exists in the metadata; otherwise, false.
    /// </returns>
    public bool HasEntity(string entityName) => !string.IsNullOrEmpty(entityName) && Entities.ContainsKey(entityName);

    /// Determines whether the specified entity contains a column with the given name.
    /// <param name="entityName">The name of the entity to check for the column.</param>
    /// <param name="propertyName">The name of the property to look for in the specified table.</param>
    /// <returns>True if the entity contains the specified property; otherwise, false.</returns>
    public bool HasProperty(string entityName, string propertyName)
    {
        if (string.IsNullOrEmpty(entityName) || string.IsNullOrEmpty(propertyName) || !Entities.TryGetValue(entityName, out var entityType))
            return false;
        
        return entityType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance) != null;
    }
    
    /// Extracts column mappings from a configuration class associated with the specified entity type.
    /// <param name="entityType">The CLR type of the entity for which column mappings are to be extracted.</param>
    /// <returns>A dictionary containing column mappings, where the key represents the original column name, and the value represents the mapped column name. Returns an empty dictionary if no configuration class is found or an error occurs during processing.
    public Dictionary<string, string> ExtractColumnMappingsFromConfiguration(Type entityType)
    {
        try
        {
            var instance = GetEntityTypeMetaInfo(entityType);
            // Create an instance of the configuration and get the mappings
            return instance is not { } metaInfo ? new Dictionary<string, string>() : metaInfo.GetColumnMappings();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Failed to extract column mappings for {entityType.Name}: {ex.Message}");
            return new Dictionary<string, string>();
        }
    }

    public IEntityTypeMetaInfo? GetEntityTypeMetaInfo(Type entityType)
    {
        // Find the configuration class that implements IEntityTypeMetaInfo for this entity type
        var configurationInterface = typeof(IEntityTypeConfiguration<>).MakeGenericType(entityType);
        var configurationType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(t =>
                t.GetInterfaces().Any(i => i == configurationInterface) &&
                t.GetInterfaces().Contains(typeof(IEntityTypeMetaInfo)));

        return configurationType == null
            ? null
            : Activator.CreateInstance(configurationType) as IEntityTypeMetaInfo;
    }
    
    public Dictionary<TKey, TResult> MapMetadata<TKey, TResult>(
        Dictionary<TKey, Type> sourceMap,
        Func<TKey, IEntityTypeMetaInfo, (TKey, TResult)> selector) where TKey : notnull
    {
        var mappings = new Dictionary<TKey, TResult>();

        foreach (var (key, entityType) in sourceMap)
        {
            try
            {
                var metadata = GetEntityTypeMetaInfo(entityType);
                if (metadata is null)
                    throw new InvalidOperationException($"Failed to get metadata for entity {entityType.Name}");

                var (newKey, result) = selector(key, metadata);
                mappings[newKey] = result;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to get metadata for entity {entityType.Name}", ex);
            }
        }

        return mappings;
    }
    
    
    /// Retrieves the default table mappings used for converting Excel data to a database representation.
    /// The mappings define the relationship between the original Excel table names
    /// and the corresponding database table names.
    /// <returns>
    /// A dictionary containing the default mappings where each key represents the original Excel table name
    /// and the value represents the corresponding database table name.
    /// </returns>
    public Dictionary<string, string> GetTableMappings() =>
        MapMetadata(
            SheetEntityMap,
            (sheetName, metadata) =>
            {
                var qualifiedTableName = !string.IsNullOrEmpty(metadata.GetSchemaName())
                    ? $"{metadata.GetSchemaName()}.{metadata.GetTableName()}"
                    : metadata.GetTableName();
                
                return (
                    sheetName,
                    qualifiedTableName);
            });


    /// Retrieves the default entity mappings for converting Excel data to database entities.
    /// This method returns a dictionary where the keys represent the names of Excel tables
    /// and the values are the corresponding CLR types representing the database entities.
    /// These mappings are used to facilitate the transformation of Excel data into
    /// strongly-typed database entities.
    /// <returns>
    /// A dictionary where the key is a string representing the Excel table name,
    /// and the value is a Type representing the corresponding entity class.
    /// </returns>
    public Dictionary<string, Type> GetDefaultEntityMappings() =>
        MapMetadata(
            SheetEntityMap, 
            (sheetName, metadata) =>
            {
                var qualifiedTableName = !string.IsNullOrEmpty(metadata.GetSchemaName()) 
                    ? $"{metadata.GetSchemaName()}.{metadata.GetTableName()}"
                    : metadata.GetTableName();
            
                return (qualifiedTableName, metadata.EntityType);
            });

    /// Retrieves the default column mappings for use in converting Excel data to database-compatible formats.
    /// This method creates a dictionary that maps the column names in the source data
    /// to their corresponding column names in the database schema.
    /// The mappings are derived using table and entity configuration provided within the application.
    /// return A dictionary where the key is the table name in the source data,
    /// and the value is another dictionary that maps source column names to destination column names.
    public  Dictionary<string, Dictionary<string, string>> GetColumnMappings()
    {
        var mappings = new Dictionary<string, Dictionary<string, string>>();

        // Get the table and entity mappings
        var tableMappings = GetTableMappings();
        var entityMappings = GetDefaultEntityMappings();

        // For each table mapping, find the corresponding entity and extract column mappings
        foreach (var (originalTableName, mappedTableName) in tableMappings)
        {
            if (!entityMappings.TryGetValue(mappedTableName, out var entityType))
            {
                // If no entity mapping found, use empty mappings (direct column name mapping)
                mappings[originalTableName] = new Dictionary<string, string>();
                continue;
            }

            mappings[originalTableName] = ExtractColumnMappingsFromConfiguration(entityType);
        }

        return mappings;
    }
}