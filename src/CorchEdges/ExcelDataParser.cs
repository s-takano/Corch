using System.Data;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
// For .xlsx files
// For .xls files
using System.Globalization;

namespace CorchEdges;

public sealed class ExcelDataParser : IExcelParser
{
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