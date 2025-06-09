using System.Data;
using CorchEdges.Data.Abstractions;
using CorchEdges.Data.Entities;
using CorchEdges.Data.Mappers;
using CorchEdges.Data.Normalizers;
using CorchEdges.Data.Providers;
using Microsoft.EntityFrameworkCore;

namespace CorchEdges.Data;

/// <summary>
/// A utility class that provides functionality to convert data from an Excel DataSet
/// to a format suitable for a relational database, adapting table and column mappings
/// between the input source and the target database schema.
/// </summary>
/// <remarks>
/// This class implements the <see cref="IDataSetConverter"/> interface to facilitate
/// data normalization and mapping transformations while integrating Excel data into
/// a database system. The mappings used can leverage custom entity-type configurations,
/// table name mappings, and column mappings as required for database compatibility.
/// </remarks>
public class ExcelToDatabaseConverter : IDataSetConverter
{
    /// <summary>
    /// Provides functionality to map original table names to their corresponding mapped names
    /// using rules or mappings defined by an <see cref="ITableNameMapper"/> implementation.
    /// This is used during the conversion process to ensure consistent and standardized
    /// table naming when working with external data sources such as Excel sheets.
    /// </summary>
    private readonly ITableNameMapper _tableMapper;

    /// <summary>
    /// An instance of the <see cref="IDataNormalizer"/> interface used for normalizing data types
    /// in DataTables to match the schema requirements of the target database table.
    /// </summary>
    private readonly IDataNormalizer _dataNormalizer;

   
    // Constructor for backward compatibility (creates default implementations)
    /// <summary>
    /// Responsible for converting Excel data into corresponding database entries using
    /// table, column, and entity mappings for seamless integration into the target database architecture.
    /// </summary>
    /// <remarks>
    /// This class is designed to normalize and map Excel data into structured database entries.
    /// It utilizes table, entity, and column mappers for flexible customization, and is constructed
    /// with default implementations to support backward compatibility. This implementation is primarily
    /// used in scenarios where structured data extraction from Excel files is necessary for database storage.
    /// </remarks>
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
    /// Provides functionality to convert data from Excel sheets or workbooks into database entries.
    /// This class allows customization of how Excel tables, columns,
    /// and entities are mapped to corresponding database tables and fields.
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

    /// <summary>
    /// Converts an Excel-derived DataSet into a database-ready format by mapping table names
    /// and normalizing data according to predefined or custom mappings.
    /// </summary>
    /// <remarks>
    /// This class implements the IDataSetConverter interface, enabling conversion of DataSets
    /// into a format that aligns with specific database schemas. It supports both predefined
    /// default mappings and user-defined custom mappings for table and column processing.
    /// </remarks>
    public ExcelToDatabaseConverter(ITableNameMapper tableMapper, IDataNormalizer dataNormalizer)
    {
        _tableMapper = tableMapper;
        _dataNormalizer = dataNormalizer;
    }


    /// <summary>
    /// Converts the provided DataSet into a format suitable for database processing.
    /// </summary>
    /// <param name="sourceDataSet">
    /// The source DataSet to be converted.
    /// </param>
    /// <returns>
    /// A new DataSet with tables mapped and data normalized for database storage.
    /// </returns>
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

    /// Retrieves the default table mappings used for converting Excel data to a database representation.
    /// The mappings define the relationship between the original Excel table names
    /// and the corresponding database table names.
    /// <returns>
    /// A dictionary containing the default mappings where each key represents the original Excel table name
    /// and the value represents the corresponding database table name.
    /// </returns>
    private static Dictionary<string, string> GetDefaultTableMappings() => new()
    {
        { "新規to業務管理", "contract_creation" },
        { "契約一覧to業務管理", "contract_current" },
        { "更新to業務管理", "contract_renewal" },
        { "解約to業務管理", "contract_termination" },
        { "processing_log", "processing_log" },
        { "processed_file", "processed_file" }
    };

    /// Retrieves the default entity mappings for converting Excel data to database entities.
    /// This method returns a dictionary where the keys represent the names of Excel tables
    /// and the values are the corresponding CLR types representing the database entities.
    /// These mappings are used to facilitate the transformation of Excel data into
    /// strongly-typed database entities.
    /// <returns>
    /// A dictionary where the key is a string representing the Excel table name,
    /// and the value is a Type representing the corresponding entity class.
    /// </returns>
    private static Dictionary<string, Type> GetDefaultEntityMappings() => new()
    {
        { "contract_creation", typeof(ContractCreation) },
        { "contract_current", typeof(ContractCurrent) },
        { "contract_renewal", typeof(ContractRenewal) },
        { "contract_termination", typeof(ContractTermination) },
        { "processing_log", typeof(ProcessingLog) },
        { "processed_file", typeof(ProcessedFile) }
    };

    /// Retrieves the default column mappings for use in converting Excel data to database-compatible formats.
    /// This method creates a dictionary that maps the column names in the source data
    /// to their corresponding column names in the database schema.
    /// The mappings are derived using table and entity configuration provided within the application.
    /// return A dictionary where the key is the table name in the source data,
    /// and the value is another dictionary that maps source column names to destination column names.
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

    /// Extracts column mappings from a configuration class associated with the specified entity type.
    /// <param name="entityType">The CLR type of the entity for which column mappings are to be extracted.</param>
    /// <returns>A dictionary containing column mappings, where the key represents the original column name, and the value represents the mapped column name. Returns an empty dictionary if no configuration class is found or an error occurs during processing.
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