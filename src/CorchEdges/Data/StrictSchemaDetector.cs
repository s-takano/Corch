using System.Data;
using CorchEdges.Data.Abstractions;
using CorchEdges.Data.Providers;

namespace CorchEdges.Data;

/// <summary>
/// Provides logic to strictly identify the database entity type for an Excel sheet
/// by comparing its headers against known entity configurations.
/// </summary>
public class StrictSchemaDetector
{
    private readonly IEntityMetadataProvider _metadataProvider;
    private readonly HashSet<string> _ignoredColumns;

    /// <summary>
    /// Initializes a new instance of the <see cref="StrictSchemaDetector"/> class.
    /// </summary>
    /// <param name="metadataProvider">The metadata provider for entity configurations.</param>
    /// <param name="ignoredColumns">Optional list of columns to ignore during matching (e.g., system columns like 'id').</param>
    public StrictSchemaDetector(IEntityMetadataProvider metadataProvider, IEnumerable<string>? ignoredColumns = null)
    {
        _metadataProvider = metadataProvider ?? throw new ArgumentNullException(nameof(metadataProvider));
        _ignoredColumns = ignoredColumns?.ToHashSet(StringComparer.OrdinalIgnoreCase) 
                         ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "id", "ProcessedFileId" };
    }

    /// <summary>
    /// Detects the qualified entity name (schema.table) for a given DataTable
    /// by performing a strict header comparison.
    /// </summary>
    /// <param name="table">The Excel sheet data as a DataTable.</param>
    /// <returns>A string representing the qualified table name (e.g., 'corch_edges_raw.contract_current').</returns>
    /// <exception cref="ArgumentException">Thrown when no matching configuration is found.</exception>
    public string DetectQualifiedEntityName(DataTable table)
    {
        var sheetName = table.TableName;
        var incomingHeaders = table.Columns.Cast<DataColumn>()
            .Select(c => c.ColumnName.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Retrieve all potential configurations for this specific sheet name
        var possibleConfigs = _metadataProvider.GetEntityTypeMetaInfoBySheetName()[sheetName];

        foreach (var config in possibleConfigs)
        {
            // Extract the expected Excel column names from the metadata, filtering out ignored ones
            var expectedHeaders = config.GetColumnMetadata()
                .Select(m => m.ColumnName) 
                .Where(name => !_ignoredColumns.Contains(name))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // STRICT MATCH: The set of headers in Excel must exactly match the set in the configuration
            if (expectedHeaders.Any() && expectedHeaders.SetEquals(incomingHeaders))
            {
                var schema = config.GetSchemaName();
                var tableName = config.GetTableName();

                return !string.IsNullOrEmpty(schema)
                    ? $"{schema}.{tableName}"
                    : tableName;
            }
        }

        throw new ArgumentException(
            $"No strict schema match found for sheet '{sheetName}'. " +
            $"The provided columns do not match any known entity configuration for this sheet name.");
    }
}
