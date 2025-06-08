using System.Data;
using CorchEdges.Data.Abstractions;
using CorchEdges.Data.Entities;
using CorchEdges.Data.Mappers;
using CorchEdges.Data.Normalizers;
using CorchEdges.Data.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Linq.Expressions;

namespace CorchEdges.Data;


public class ExcelToDatabaseConverter : IDataSetConverter
{
    private readonly ITableNameMapper _tableMapper;
    private readonly IDataNormalizer _dataNormalizer;

   
    // Constructor for backward compatibility (creates default implementations)
    public ExcelToDatabaseConverter()
    {
        var tableMappings = GetDefaultTableMappings();
        var entityMappings = GetDefaultEntityMappings();
        var columnMappings = GetDefaultColumnMappings();

        _tableMapper = new EntityBasedTableMapper(tableMappings);
        IEntityMetadataProvider metadataProvider = new ReflectionEntityMetadataProvider(entityMappings);
        var columnMapper = new EntityBasedColumnMapper(columnMappings);
        _dataNormalizer = new EntityDataNormalizer(metadataProvider, columnMapper);
    }

    // Constructor for custom mappings (backward compatibility)
    public ExcelToDatabaseConverter(
        Dictionary<string, Type> entityTypeMappings,
        Dictionary<string, string>? tableMappings = null,
        Dictionary<string, Dictionary<string, string>>? columnMappings = null)
    {
        // Create all dependencies directly, no constructor chaining
        var tableMap = tableMappings ?? GetDefaultTableMappings();
        var columnMap = columnMappings ?? new Dictionary<string, Dictionary<string, string>>();

        _tableMapper = new EntityBasedTableMapper(tableMap);
        IEntityMetadataProvider metadataProvider = new ReflectionEntityMetadataProvider(entityTypeMappings);
        var columnMapper = new EntityBasedColumnMapper(columnMap);
        _dataNormalizer = new EntityDataNormalizer(metadataProvider, columnMapper);
    }
    

    public DataSet ConvertForDatabase(DataSet sourceDataSet)
    {
        var result = new DataSet();

        foreach (DataTable sourceTable in sourceDataSet.Tables)
        {
            if (sourceTable.Rows.Count == 0)
                continue;

            var mappedTableName = _tableMapper.MapTableName(sourceTable.TableName);
            var normalizedTable = _dataNormalizer.NormalizeTypes(mappedTableName, sourceTable);
            result.Tables.Add(normalizedTable);
        }

        return result;
    }

    // =============================================
    // DEFAULT CONFIGURATION
    // =============================================

    private static Dictionary<string, string> GetDefaultTableMappings() => new()
    {
        { "新規to業務管理", "contract_creation" },
        { "契約一覧to業務管理", "contract_current" },
        { "更新to業務管理", "contract_renewal" },
        { "解約to業務管理", "contract_termination" },
        { "processing_log", "processing_log" },
        { "processed_file", "processed_file" }
    };

    private static Dictionary<string, Type> GetDefaultEntityMappings() => new()
    {
        { "contract_creation", typeof(ContractCreation) },
        { "contract_current", typeof(ContractCurrent) },
        { "contract_renewal", typeof(ContractRenewal) },
        { "contract_termination", typeof(ContractTermination) },
        { "processing_log", typeof(ProcessingLog) },
        { "processed_file", typeof(ProcessedFile) }
    };
    private static Dictionary<string, Dictionary<string, string>> GetDefaultColumnMappings()
    {
        var mappings = new Dictionary<string, Dictionary<string, string>>();
    
        // Get the table and entity mappings
        var tableMappings = GetDefaultTableMappings();
        var entityMappings = GetDefaultEntityMappings();
    
        // For each table mapping, find the corresponding entity and extract column mappings
        foreach (var (originalTableName, mappedTableName) in tableMappings)
        {
            if (entityMappings.TryGetValue(mappedTableName, out var entityType))
            {
                mappings[originalTableName] = ExtractColumnMappingsFromConfiguration(entityType);
            }
            else
            {
                // If no entity mapping found, use empty mappings (direct column name mapping)
                mappings[originalTableName] = new Dictionary<string, string>();
            }
        }
    
        return mappings;
    }

    private static Dictionary<string, string> ExtractColumnMappingsFromConfiguration(Type entityType)
    {
        try
        {
            // Find the configuration class that implements IEntityTypeMetaInfo for this entity type
            var configurationInterface = typeof(IEntityTypeConfiguration<>).MakeGenericType(entityType);
            var configurationType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t =>
                    t.GetInterfaces().Any(i => i == configurationInterface) &&
                    t.GetInterfaces().Contains(typeof(IEntityTypeMetaInfo)));

            if (configurationType != null)
            {
                // Create an instance of the configuration and get the mappings
                if (Activator.CreateInstance(configurationType) is IEntityTypeMetaInfo metaInfo)
                {
                    return metaInfo.GetColumnMappings();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Failed to extract column mappings for {entityType.Name}: {ex.Message}");
        }

        return new Dictionary<string, string>();
    }
}