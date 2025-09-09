using System.Data;
using CorchEdges.Data.Abstractions;
using CorchEdges.Data.Mappers;
using System.Linq;

namespace CorchEdges.Data.Normalizers;

/// <summary>
/// Represents a data normalizer for transforming and validating data according
/// to metadata and column mappings appropriate for a target entity or table.
/// </summary>
/// <remarks>
/// This class facilitates the normalization of data by leveraging metadata and column mappings
/// to transform the structure and data types of a source DataTable to match the target DataTable.
/// It is particularly useful for scenarios where data consistency and alignment with
/// predefined schemas or metadata are required.
/// </remarks>
/// <example>
/// This data normalizer is designed for use within the context of systems that require
/// consistent data transformation and type mapping, such as during data migrations or
/// format standardization workflows.
/// </example>
/// <seealso cref="ITableNormalizer" />
public class TableNormalizer : ITableNormalizer
{
    /// <summary>
    /// Provides access to an implementation of <see cref="IEntityMetadataProvider"/> used to retrieve
    /// metadata about database entities, such as column types and existence of tables or columns.
    /// This is utilized to ensure data consistency and compatibility during normalization operations.
    /// </summary>
    private readonly IEntityMetadataProvider _metadataProvider;

    /// <summary>
    /// Represents the dependency responsible for mapping original column names
    /// to their desired format, as defined by the <see cref="IColumnNameMapper"/> contract.
    /// This field is used within the normalization process to transform column names
    /// according to specific mapping logic required for database or entity data handling.
    /// </summary>
    private readonly IColumnNameMapper _columnMapper;

    /// <summary>
    /// Provides functionality to normalize entity data for a target table based on
    /// metadata and configurable column mappings. This class facilitates
    /// transformations of data types and column names to conform to a specified schema.
    /// </summary>
    public TableNormalizer(IEntityMetadataProvider metadataProvider)
    {
        _metadataProvider = metadataProvider ?? throw new ArgumentNullException(nameof(metadataProvider));
        _columnMapper = new EntityBasedColumnMapper(_metadataProvider.GetColumnMappings());
    }

    private static bool IsDataConvertibleType(Type type)
    {
        // Check if the type is something we can reasonably convert data to/from
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
        return IsDataTableCompatibleType(underlyingType);
    }

    private static bool IsDataTableCompatibleType(Type type)
    {
        // DataTable supports these types directly
        return type == typeof(string) ||
               type == typeof(int) ||
               type == typeof(long) ||
               type == typeof(decimal) ||
               type == typeof(double) ||
               type == typeof(bool) ||
               type == typeof(DateTime) ||
               type == typeof(DateOnly) ||
               type == typeof(TimeOnly) ||
               type.IsEnum;
    }

    /// <summary>
    /// Normalizes the column data types of a source DataTable to match the schema of the specified target table.
    /// </summary>
    /// <param name="entityName">The name of the target table whose schema will be used to normalize column types.</param>
    /// <param name="sourceTable">The source DataTable containing the data to be normalized.</param>
    /// <returns>A new DataTable with columns converted to match the data types of the target table schema.</returns>
    public DataTable Normalize(string entityName, DataTable sourceTable)
    {
        var result = new DataTable(entityName);

        sourceTable.Columns.Cast<DataColumn>()
            .Where(dataColumn => IsDataConvertibleType(dataColumn.DataType))
            .ToList()
            .ForEach(dataColumn => result.Columns.Add(
            MapColumnDefinition(sourceTable.TableName, dataColumn.ColumnName)));

        sourceTable.Rows.Cast<DataRow>().ToList().ForEach(sourceRow => result.Rows.Add(
            ConvertRow(result, sourceRow)));

        return result;

        DataColumn MapColumnDefinition(string sheetName, string sheetColumnName)
        {
            var entityPropertyName = _columnMapper.MapColumnName(sheetName, sheetColumnName);
            var entityPropertyType = _metadataProvider.GetPropertyType(entityName, entityPropertyName);

            // Handle nullable types - DataSet doesn't support nullable types directly
            // This matches the original implementation exactly
            var dataTableColumnType = Nullable.GetUnderlyingType(entityPropertyType) ?? entityPropertyType;

            // respect the original column name 
            var column = new DataColumn(sheetColumnName, dataTableColumnType);
            // var column = dataColumnCollection.Add(sheetColumnName, dataTableColumnType);

            // Set AllowDBNull based on whether the ORIGINAL type was nullable OR a reference type
            // For value types: only allow null if it was nullable (int?, bool?, etc.)
            // For reference types: always allow null (string, object, etc.)
            var isOriginallyNullable = Nullable.GetUnderlyingType(entityPropertyType) != null;
            var isReferenceType = !entityPropertyType.IsValueType;
            column.AllowDBNull = isOriginallyNullable || isReferenceType;
            return column;
        }

        DataRow ConvertRow(DataTable targetTable, DataRow sourceRow)
        {
            var newRow = targetTable.NewRow();
            for (var i = 0; i < sourceRow.Table.Columns.Count; i++)
            {
                var targetColumn = targetTable.Columns[i];
                newRow[i] = ConvertColumnValues(
                    sourceRow[i],
                    targetColumn.ColumnName,
                    targetColumn.DataType,
                    targetColumn.AllowDBNull);
            }

            return newRow;
        }

        object ConvertColumnValues(object sourceValue, string columnName, Type dataType, bool allowDbNull)
        {
            try
            {
                if (sourceValue == DBNull.Value)
                    return allowDbNull
                        ? DBNull.Value // Only set DBNull if the column allows it
                        : GetDefaultValueForType(dataType); // For non-nullable columns, provide a default value

                var convertedValue = CastValueAsType(sourceValue, dataType, allowDbNull);

                // If conversion returned DBNull but the column doesn't allow nulls, use default
                return convertedValue != DBNull.Value || allowDbNull
                    ? convertedValue
                    : GetDefaultValueForType(dataType);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to convert value '{sourceValue}' in column '{columnName}' " +
                    $"to type {dataType.Name}: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Converts a given value to a specified target type, taking into account whether null values are allowed for the conversion.
    /// </summary>
    /// <param name="value">The value to be converted. Can be any object type.</param>
    /// <param name="targetType">The target type to convert the value to.</param>
    /// <param name="allowNull">A boolean indicating whether the target type allows null values.</param>
    /// <returns>The value converted to the specified target type, or a default/null equivalent as appropriate.</returns>
    /// <exception cref="FormatException">Thrown if the value cannot be converted to the target type due to format issues.</exception>
    /// <exception cref="InvalidCastException">Thrown if the value cannot be cast to the target type.</exception>
    /// <exception cref="ArgumentNullException">Thrown if a null or invalid parameter is provided when null values are not allowed.</exception>
    private static object CastValueAsType(object value, Type targetType, bool allowNull)
    {
        // Handle nullable types
        var underlyingTargetType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        
        return value.GetType() == underlyingTargetType ? 
            value // If value is already the correct type, return as-is 
            : // Otherwise, convert string values to appropriate types
            ConvertValueBasedOnType(value.ToString()?.Trim() ?? string.Empty, underlyingTargetType); 


        object ConvertValueBasedOnType(string stringValue, Type conversionType)
        {
            if (string.IsNullOrEmpty(stringValue))
            {
                // If column allows null, return DBNull, otherwise return default value
                return allowNull ? DBNull.Value : GetDefaultValueForType(targetType);
            }

            object? castValueAsType = conversionType.Name switch
            {
                nameof(String) => stringValue,
                nameof(Int32) => int.Parse(stringValue),
                nameof(Int64) => long.Parse(stringValue),
                nameof(Decimal) => decimal.Parse(stringValue),
                nameof(Double) => double.Parse(stringValue),
                nameof(Boolean) => ParseBoolean(stringValue),
                nameof(DateTime) => DateTime.Parse(stringValue),
                nameof(DateOnly) => ParseDateOnly(stringValue),
                nameof(TimeOnly) => ParseTimeOnly(stringValue),
                _ => null // only this path returns null
            };
            
            // when there's no explicit conversion path found, try a general converter
            return castValueAsType ?? Convert.ChangeType(value, conversionType);
        }
    }

    // Add this helper method
    /// <summary>
    /// Parses a string representation of a time and converts it into a <see cref="TimeOnly"/> object.
    /// </summary>
    /// <param name="stringValue">The string containing the time to be parsed.</param>
    /// <returns>A <see cref="TimeOnly"/> object representing the parsed time.</returns>
    /// <exception cref="FormatException">Thrown when the input string cannot be parsed as a valid time.</exception>
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

    /// Parses the input string into a DateOnly instance, handling various string formats.
    /// <param name="stringValue">The string value to be parsed into a DateOnly instance.</param>
    /// <returns>A DateOnly instance parsed from the input string.</returns>
    /// <exception cref="FormatException">Thrown when the input string cannot be parsed as a DateOnly instance.</exception>
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

    /// <summary>
    /// Parses the given string value into a boolean.
    /// </summary>
    /// <param name="value">The string value to parse as a boolean. Supported values for true include "true", "1", "yes", "y", and "on". Supported values for false include "false", "0", "no", "n", and "off".</param>
    /// <returns>Returns true or false based on the parsed value. Throws a <see cref="FormatException"/> if the string cannot be converted.</returns>
    private static bool ParseBoolean(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "true" or "1" or "yes" or "y" or "on" => true,
            "false" or "0" or "no" or "n" or "off" => false,
            _ => throw new FormatException($"Unable to parse '{value}' as boolean")
        };
    }

    /// Returns the default value for the specified type. If the provided type is nullable, it resolves to its underlying type and provides the appropriate default value.
    /// <param name="type">The type for which the default value is required. This can be a nullable or non-nullable type.</param>
    /// <return>An instance representing the default value of the specified type. For nullable types, a default value is provided based on the underlying type. For reference types, this is null unless otherwise specified (e.g., empty string for String).</return>
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