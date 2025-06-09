using System.Data;
using System.Globalization;
using CorchEdges.Abstractions;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
// For .xlsx files
// For .xls files

namespace CorchEdges.Utilities;

/// <summary>
/// Provides functionality for parsing Excel files (.xlsx or .xls) and extracting their content as a DataSet.
/// </summary>
/// <remarks>
/// This class is responsible for reading Excel files provided as byte arrays and returning their content
/// as a DataSet. Each worksheet from the Excel file is processed into a DataTable, and only sheets with valid
/// data and meaningful column headers are included in the resulting DataSet.
/// </remarks>
public sealed class ExcelDataParser : IExcelParser
{
    /// Parses an Excel file given as a byte array into a DataSet containing tables for each valid worksheet.
    /// In case of failure, an error message is returned.
    /// <param name="bytes">The byte array representing the Excel file to be parsed.</param>
    /// <returns>
    /// A tuple containing:
    /// 1. A DataSet object with tables corresponding to the valid worksheets in the Excel file, or null if parsing fails.
    /// 2. A string with an error message if parsing fails, or null if parsing is successful.
    /// </returns>
    public (DataSet?, string?) Parse(byte[] bytes)
    {
        try
        {
            var dataSet = new DataSet();

            using var stream = new MemoryStream(bytes);
            using var workbook = new XSSFWorkbook(stream);

            for (int sheetIndex = 0; sheetIndex < workbook.NumberOfSheets; sheetIndex++)
            {
                var sheet = workbook.GetSheetAt(sheetIndex);
                if (sheet.PhysicalNumberOfRows == 0) continue;

                var table = CreateDataTableFromSheetWithValidColumns(sheet);
                if (table.Columns.Count > 0) // Only add tables with meaningful columns
                {
                    dataSet.Tables.Add(table);
                }
            }

            return (dataSet.Tables.Count > 0 ? dataSet : null, null);
        }
        catch (Exception ex)
        {
            return (null, $"Failed to parse Excel file: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a <see cref="DataTable"/> from a given Excel sheet containing only valid and meaningful columns.
    /// Meaningful columns are determined by the presence of non-empty and trimmed headers in the first row of the sheet.
    /// Rows are processed starting from the second row, and only data within meaningful columns is included.
    /// </summary>
    /// <param name="sheet">An Excel worksheet represented as an <see cref="ISheet"/> object from the NPOI library.</param>
    /// <returns>A <see cref="DataTable"/> representation of the Excel sheet. If no meaningful columns are found, an empty <see cref="DataTable"/> is returned.</returns>
    private DataTable CreateDataTableFromSheetWithValidColumns(ISheet sheet)
    {
        var table = new DataTable(sheet.SheetName);
        var headerRow = sheet.GetRow(0);

        if (headerRow == null) return table;

        // First pass: identify meaningful columns and their indices
        var meaningfulColumnMappings = new List<(int originalIndex, string columnName)>();

        for (int cellIndex = 0; cellIndex < headerRow.LastCellNum; cellIndex++)
        {
            var cell = headerRow.GetCell(cellIndex);
            var headerValue = GetCellValueAsString(cell)?.Trim();

            // Only include columns with meaningful headers
            if (!string.IsNullOrWhiteSpace(headerValue))
            {
                meaningfulColumnMappings.Add((cellIndex, headerValue));
                table.Columns.Add(headerValue);
            }
        }

        // If no meaningful columns found, return empty table
        if (meaningfulColumnMappings.Count == 0)
            return table;

        // Second pass: process data rows using only meaningful columns
        for (int rowIndex = 1; rowIndex <= sheet.LastRowNum; rowIndex++)
        {
            var dataRow = sheet.GetRow(rowIndex);
            if (dataRow == null) continue;

            var newRow = table.NewRow();

            for (int colIndex = 0; colIndex < meaningfulColumnMappings.Count; colIndex++)
            {
                var (originalCellIndex, _) = meaningfulColumnMappings[colIndex];
                var cell = dataRow.GetCell(originalCellIndex);
                newRow[colIndex] = GetCellValue(cell);
            }

            table.Rows.Add(newRow);
        }

        return table;
    }

    /// Retrieves the value of the provided Excel cell as a string representation.
    /// <param name="cell">The Excel cell from which to retrieve the value. Can be null.</param>
    /// <return>
    /// A string representation of the cell's value if the cell is not null; otherwise, null.
    /// The return value depends on the type of the cell:
    /// - For string cells, the cell content.
    /// - For numeric cells, the numeric value as a string.
    /// - For boolean cells, the boolean value as a string ("True" or "False").
    /// - For formula cells, the formula's evaluated value as a string if retrievable.
    /// - For other cell types or invalid states, null.
    /// </return>
    private string? GetCellValueAsString(ICell? cell)
    {
        if (cell == null) return null;

        return cell.CellType switch
        {
            CellType.String => cell.StringCellValue,
            CellType.Numeric => cell.NumericCellValue.ToString(),
            CellType.Boolean => cell.BooleanCellValue.ToString(),
            CellType.Formula => GetFormulaValue(cell) as string,
            _ => null
        };
    }

    /// <summary>
    /// Retrieves the value of a given Excel cell, handling various cell types such as
    /// strings, numbers, dates, booleans, and formulas.
    /// </summary>
    /// <param name="cell">The Excel cell for which the value needs to be retrieved. If the cell is null, a DBNull value is returned.</param>
    /// <returns>
    /// The value of the cell. The return type can vary depending on the content of the cell:
    /// - For string cells, returns a string value.
    /// - For numeric cells, returns a double or formatted date string if the cell contains a date.
    /// - For boolean cells, returns a boolean value.
    /// - For formula cells, returns the computed result of the formula.
    /// - If none of the above, returns DBNull.
    /// </returns>
    private object? GetCellValue(ICell? cell)
    {
        if (cell == null) return DBNull.Value;

        return cell.CellType switch
        {
            CellType.String => cell.StringCellValue,
            CellType.Numeric when DateUtil.IsCellDateFormatted(cell) => FormatDateConsistently(cell.DateCellValue),
            CellType.Numeric => cell.NumericCellValue,
            CellType.Boolean => cell.BooleanCellValue,
            CellType.Formula => GetFormulaValue(cell),
            _ => DBNull.Value
        };
    }

    /// <summary>
    /// Formats a given DateTime value into a consistent string representation.
    /// If the time component is midnight (00:00:00), it returns the date-only format.
    /// Otherwise, it includes the date and time in the resultant string.
    /// </summary>
    /// <param name="dateTime">The DateTime value to be formatted. Can be null.</param>
    /// <returns>
    /// A formatted string representing the date in a consistent format.
    /// If the input is null, an empty string is returned.
    /// </returns>
    private string FormatDateConsistently(DateTime? dateTime)
    {
        // Consistent date formatting - preserve only date if time is midnight
        if (!dateTime.HasValue) return string.Empty;

        var dt = dateTime.Value;

        // If time is midnight (00:00:00), return date-only format
        if (dt.TimeOfDay == TimeSpan.Zero)
        {
            return dt.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
        }
        else
        {
            return dt.ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture);
        }
    }

    /// Retrieves the value of a formula cell by evaluating its cached result.
    /// <param name="cell">The cell containing the formula whose value needs to be evaluated.</param>
    /// <returns>The evaluated value of the formula as an object, which may be of type string, double, DateTime, bool, or DBNull.Value if the evaluation fails.</returns>
    private object? GetFormulaValue(ICell cell)
    {
        try
        {
            return cell.CachedFormulaResultType switch
            {
                CellType.String => cell.StringCellValue,
                CellType.Numeric when DateUtil.IsCellDateFormatted(cell) =>
                    cell.DateCellValue?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                CellType.Numeric => cell.NumericCellValue,
                CellType.Boolean => cell.BooleanCellValue,
                _ => DBNull.Value
            };
        }
        catch
        {
            return DBNull.Value;
        }
    }
}