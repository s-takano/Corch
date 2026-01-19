using System.Data;
using CorchEdges.Data.Abstractions;
using CorchEdges.Data.Providers;

namespace CorchEdges.Data;

public sealed record DetectedEntity(string QualifiedTableName, IEntityTypeMetaInfo Configuration);

public class StrictSchemaDetector
{
    private readonly IEntityMetadataProvider _metadataProvider;
    private readonly HashSet<string> _ignoredColumns;

    public StrictSchemaDetector(IEntityMetadataProvider metadataProvider, IEnumerable<string>? ignoredColumns = null)
    {
        _metadataProvider = metadataProvider ?? throw new ArgumentNullException(nameof(metadataProvider));
        _ignoredColumns = ignoredColumns?.ToHashSet(StringComparer.OrdinalIgnoreCase)
                         ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "id", "ProcessedFileId" };
    }

    public DetectedEntity DetectQualifiedEntityWithConfiguration(DataTable table)
    {
        var sheetName = table.TableName;
        var incomingHeaders = table.Columns.Cast<DataColumn>()
            .Select(c => c.ColumnName.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var possibleConfigs = _metadataProvider.GetEntityTypeMetaInfoBySheetName()[sheetName];

        foreach (var config in possibleConfigs)
        {
            var expectedHeaders = config.GetColumnMetadata()
                .Select(m => m.ColumnName)
                .Where(name => !_ignoredColumns.Contains(name))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (expectedHeaders.Any() && expectedHeaders.SetEquals(incomingHeaders))
            {
                var schema = config.GetSchemaName();
                var tableName = config.GetTableName();

                var qualified = !string.IsNullOrEmpty(schema)
                    ? $"{schema}.{tableName}"
                    : tableName;

                return new DetectedEntity(qualified, config);
            }
        }

        throw new ArgumentException(
            $"No strict schema match found for sheet '{sheetName}'. " +
            $"The provided columns do not match any known entity configuration for this sheet name.");
    }

    public string DetectQualifiedEntityName(DataTable table)
    {
        return DetectQualifiedEntityWithConfiguration(table).QualifiedTableName;
    }
}
