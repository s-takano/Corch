using System.Data;
using CorchEdges.Data.Abstractions;
using CorchEdges.Data.Normalizers;
using CorchEdges.Data.Providers;

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
    /// An instance of the <see cref="ITableNormalizer"/> interface used for normalizing data types
    /// in DataTables to match the schema requirements of the target database table.
    /// </summary>
    private readonly ITableNormalizer _tableNormalizer;

    private readonly IEntityMetadataProvider _metadataProvider;


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
        _metadataProvider = new ReflectionEntityMetadataProvider();
        _tableNormalizer = new TableNormalizer(_metadataProvider);
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

            var entityName = MapSheetNameToEntityName(sourceTable.TableName);
            var normalizedTable = _tableNormalizer.Normalize(entityName, sourceTable);
            result.Tables.Add(normalizedTable);
        }

        return result;
    }


    private string MapSheetNameToEntityName(string originalTableName)
    {
        if (!string.IsNullOrWhiteSpace(originalTableName) &&
            _metadataProvider.GetTableMappings().TryGetValue(originalTableName, out var mapped))
            return mapped;

        throw new ArgumentException($"Invalid table name: {originalTableName}");
    }
}