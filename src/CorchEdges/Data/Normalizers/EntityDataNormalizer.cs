using System.Data;
using CorchEdges.Data.Abstractions;

namespace CorchEdges.Data.Normalizers;

public class EntityDataNormalizer : IDataNormalizer
{
    private readonly IEntityMetadataProvider _metadataProvider;
    private readonly IColumnNameMapper _columnMapper;

    public EntityDataNormalizer(IEntityMetadataProvider metadataProvider, IColumnNameMapper columnMapper)
    {
        _metadataProvider = metadataProvider ?? throw new ArgumentNullException(nameof(metadataProvider));
        _columnMapper = columnMapper ?? throw new ArgumentNullException(nameof(columnMapper));
    }

    public DataTable NormalizeTypes(string targetTableName, DataTable sourceTable)
    {
        var result = new DataTable(targetTableName);

        // Create columns with correct types (matching original logic exactly)
        for (int colIndex = 0; colIndex < sourceTable.Columns.Count; colIndex++)
        {
            var originalColumnName = sourceTable.Columns[colIndex].ColumnName;
            var validatedColumnName = _columnMapper.MapColumnName(sourceTable.TableName, originalColumnName);
            var columnType = _metadataProvider.GetColumnType(targetTableName, validatedColumnName);
            
            // Handle nullable types - DataSet doesn't support nullable types directly
            // This matches the original implementation exactly
            var dataTableColumnType = Nullable.GetUnderlyingType(columnType) ?? columnType;

            var column = result.Columns.Add(validatedColumnName, dataTableColumnType);
            
            // Set AllowDBNull based on whether the ORIGINAL type was nullable OR a reference type
            // For value types: only allow null if it was nullable (int?, bool?, etc.)
            // For reference types: always allow null (string, object, etc.)
            var isOriginallyNullable = Nullable.GetUnderlyingType(columnType) != null;
            var isReferenceType = !columnType.IsValueType;
            column.AllowDBNull = isOriginallyNullable || isReferenceType;
        }

        // Convert and copy data
        foreach (DataRow sourceRow in sourceTable.Rows)
        {
            var newRow = result.NewRow();

            for (int colIndex = 0; colIndex < sourceTable.Columns.Count; colIndex++)
            {
                var sourceValue = sourceRow[colIndex];
                var targetColumn = result.Columns[colIndex];
                var originalColumnName = sourceTable.Columns[colIndex].ColumnName;
                var validatedColumnName = _columnMapper.MapColumnName(sourceTable.TableName, originalColumnName);
                var originalColumnType = _metadataProvider.GetColumnType(targetTableName, validatedColumnName);

                if (sourceValue == null || sourceValue == DBNull.Value)
                {
                    // Only set DBNull if the column allows it
                    if (targetColumn.AllowDBNull)
                    {
                        newRow[colIndex] = DBNull.Value;
                    }
                    else
                    {
                        // For non-nullable columns, provide a default value
                        newRow[colIndex] = GetDefaultValueForType(targetColumn.DataType);
                    }
                }
                else
                {
                    try
                    {
                        var convertedValue = ConvertValueToType(sourceValue, targetColumn.DataType, targetColumn.AllowDBNull);
                        
                        // If conversion returned DBNull but column doesn't allow nulls, use default
                        if (convertedValue == DBNull.Value && !targetColumn.AllowDBNull)
                        {
                            newRow[colIndex] = GetDefaultValueForType(targetColumn.DataType);
                        }
                        else
                        {
                            newRow[colIndex] = convertedValue;
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            $"Failed to convert value '{sourceValue}' in column '{targetColumn.ColumnName}' " +
                            $"to type {targetColumn.DataType.Name}: {ex.Message}", ex);
                    }
                }
            }

            result.Rows.Add(newRow);
        }

        return result;
    }

    private static object ConvertValueToType(object value, Type targetType, bool allowNull)
    {
        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        // If value is already the correct type, return as-is
        if (value.GetType() == underlyingType)
        {
            return value;
        }

        // Convert string values to appropriate types
        var stringValue = value.ToString()?.Trim();

        if (string.IsNullOrEmpty(stringValue))
        {
            // If column allows null, return DBNull, otherwise return default value
            return allowNull ? DBNull.Value : GetDefaultValueForType(targetType);
        }

        return underlyingType.Name switch
        {
            nameof(String) => stringValue,
            nameof(Int32) => int.Parse(stringValue),
            nameof(Int64) => long.Parse(stringValue),
            nameof(Decimal) => decimal.Parse(stringValue),
            nameof(Double) => double.Parse(stringValue),
            nameof(Boolean) => ParseBoolean(stringValue),
            nameof(DateTime) => DateTime.Parse(stringValue),
            nameof(DateOnly) => ParseDateOnly(stringValue),
            nameof(TimeOnly) => ParseTimeOnly(stringValue), // ✅ Add custom parser
            _ => Convert.ChangeType(value, underlyingType)
        };
    }

    // Add this helper method
    private static TimeOnly ParseTimeOnly(string stringValue)
    {
        // First try: Parse as DateTime and extract time part (handles full datetime strings)
        if (DateTime.TryParse(stringValue, out var dateTime))
        {
            return TimeOnly.FromDateTime(dateTime);
        }
        
        // Second try: Parse directly as TimeOnly (for time-only strings like "14:30:15")
        if (TimeOnly.TryParse(stringValue, out var timeOnly))
        {
            return timeOnly;
        }
        
        // Third try: Extract time part manually for formats like "2025/05/07 9:10:04"
        try
        {
            // Split by space and take the second part (time part)
            var parts = stringValue.Split(new[] { ' ', 'T' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                return TimeOnly.Parse(parts[1]);
            }
        
            // If only one part, assume it's time
            return TimeOnly.Parse(stringValue);
        }
        catch
        {
            throw new FormatException($"Unable to parse '{stringValue}' as TimeOnly");
        }
    }

    private static DateOnly ParseDateOnly(string stringValue)
    {
        // First try: Parse as DateTime and extract date part (handles strings with time)
        if (DateTime.TryParse(stringValue, out var dateTime))
        {
            return DateOnly.FromDateTime(dateTime);
        }
        
        // Second try: Parse directly as DateOnly (for date-only strings)
        if (DateOnly.TryParse(stringValue, out var dateOnly))
        {
            return dateOnly;
        }
        
        // Third try: Extract date part manually for formats like "2025/05/07 9:10:04"
        try
        {
            // Split by space or 'T' and take the first part (date part)
            var datePart = stringValue.Split(new[] { ' ', 'T' }, StringSplitOptions.RemoveEmptyEntries)[0];
            return DateOnly.Parse(datePart);
        }
        catch
        {
            throw new FormatException($"Unable to parse '{stringValue}' as DateOnly");
        }
    }

    private static bool ParseBoolean(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "true" or "1" or "yes" or "y" or "on" => true,
            "false" or "0" or "no" or "n" or "off" => false,
            _ => throw new FormatException($"Unable to parse '{value}' as boolean")
        };
    }

    private static object GetDefaultValueForType(Type type)
    {
        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        return underlyingType.Name switch
        {
            nameof(String) => string.Empty,
            nameof(Int32) => 0,
            nameof(Int64) => 0L,
            nameof(Decimal) => 0m,
            nameof(Double) => 0.0,
            nameof(Boolean) => false,
            nameof(DateTime) => DateTime.MinValue,
            nameof(DateOnly) => DateOnly.MinValue,
            nameof(TimeOnly) => TimeOnly.MinValue,
            _ => Activator.CreateInstance(underlyingType) ?? DBNull.Value
        };
    }
}