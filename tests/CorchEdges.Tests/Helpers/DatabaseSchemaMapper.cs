using System.Data;

namespace CorchEdges.Tests.Helpers;

public static class DatabaseTestHelper
{
    public static string GetTableDefinition(DataTable table)
    {
        var columns = new List<string>();

        foreach (DataColumn column in table.Columns)
        {
            var columnName = $"\"{column.ColumnName}\"";
            var dataType = GetPostgresDataType(column.DataType);
            columns.Add($"{columnName} {dataType}");
        }

        return string.Join(", ", columns);
    }

    private static string GetPostgresDataType(Type dotNetType)
    {
        return dotNetType.Name switch
        {
            nameof(String) => "TEXT",
            nameof(Int32) => "INTEGER",
            nameof(Int64) => "BIGINT",
            nameof(Decimal) => "DECIMAL(18,2)",
            nameof(Double) => "DOUBLE PRECISION",
            nameof(Boolean) => "BOOLEAN",
            nameof(DateTime) => "TIMESTAMP",
            nameof(DateOnly) => "DATE",
            _ => "TEXT"
        };
    }
}