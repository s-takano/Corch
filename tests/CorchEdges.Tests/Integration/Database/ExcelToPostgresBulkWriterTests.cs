using System.Data;
using CorchEdges.Data;
using CorchEdges.Data.Abstractions;
using CorchEdges.Tests.Helpers;
using CorchEdges.Utilities;
using Xunit;

namespace CorchEdges.Tests.Integration.Database;

public class ExcelToPostgresBulkWriterTests : PostgresDatabaseTestBase
{
    protected override string TestSchema { get;  } = "corch_edges_raw";
    
    private readonly IPostgresTableWriter _writer = new PostgresTableWriter();
    private readonly ExcelDataParser _excelParser = new ExcelDataParser();
    private readonly IDataSetConverter _dataSetConverter = new ExcelToDatabaseConverter();
        

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Source", "Excel")]
    public async Task WriteAsync_FromValidExcelFile_InsertsDataSuccessfully()
    {
        // Arrange
        var excelFilePath = Path.Combine("TestData", "valid-data.xlsx");
        Assert.True(File.Exists(excelFilePath), $"Test file not found: {excelFilePath}");

        byte[] excelBytes = await File.ReadAllBytesAsync(excelFilePath);
        var (sourceDataSet, _) = _excelParser.Parse(excelBytes);

        Assert.NotNull(sourceDataSet);
        Assert.True(sourceDataSet.Tables.Count > 0, "Excel file should contain at least one table");

        // Prepare data for database
        var preparedDataSet = _dataSetConverter.ConvertForDatabase(sourceDataSet);
        var createdTables = await CreateDatabaseTablesFromDataSet(preparedDataSet);

        await using var transaction = await Connection.BeginTransactionAsync();

        // Act
        await _writer.WriteAsync(preparedDataSet, Connection, transaction);
        await transaction.CommitAsync();

        // Assert
        foreach (var (originalName, mappedName, qualifiedName) in createdTables)
        {
            var rowCount = await GetTableRowCount(qualifiedName);
            Assert.True(rowCount > 0, $"Table {originalName} -> {mappedName} should have data");

            var tableData = await GetTableData(qualifiedName);
            Assert.True(tableData.Count > 0, $"Should be able to read data from {mappedName}");
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Source", "Excel")]
    public async Task WriteAsync_FromValidExcelFile_DataIntegrityCheck()
    {
        // Arrange
        var excelFilePath = Path.Combine("TestData", "valid-data.xlsx");
        byte[] excelBytes = await File.ReadAllBytesAsync(excelFilePath);
        var (sourceDataSet, _) = _excelParser.Parse(excelBytes);

        var preparedDataSet = _dataSetConverter.ConvertForDatabase(sourceDataSet!);
        var sourceTable = preparedDataSet.Tables.Cast<DataTable>().First(t => t.Rows.Count > 0);

        // Store original data for comparison
        var originalRows = ExtractRowData(sourceTable);

        // Create database table
        var tableDefinition = DatabaseTestHelper.GetTableDefinition(sourceTable);
        var qualifiedTableName = await SetupTestTable(sourceTable.TableName, tableDefinition);
            
        // Create a copy of the table with the qualified name for database operations
        var tableForDatabase = sourceTable.Copy();
        tableForDatabase.TableName = qualifiedTableName;

        await using var transaction = await Connection.BeginTransactionAsync();

        // Act
        var singleTableDataSet = new DataSet();
        singleTableDataSet.Tables.Add(tableForDatabase); // Now using a copy
        await _writer.WriteAsync(singleTableDataSet, Connection, transaction);
        await transaction.CommitAsync();

        // Assert
        var insertedData = await GetTableData(qualifiedTableName);
        Assert.Equal(originalRows.Count, insertedData.Count);

        VerifyDataIntegrity(originalRows, insertedData, tableForDatabase);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Source", "Excel")]
    public async Task WriteAsync_AllContractSheetsMapping_CreatesCorrectTables()
    {
        // Arrange
        var testDataSet = CreateTestDataSetWithAllContractTypes();
        var preparedDataSet = _dataSetConverter.ConvertForDatabase(testDataSet);
        var createdTables = await CreateDatabaseTablesFromDataSet(preparedDataSet);

        await using var transaction = await Connection.BeginTransactionAsync();

        // Act
        await _writer.WriteAsync(preparedDataSet, Connection, transaction);
        await transaction.CommitAsync();

        // Assert
        Assert.Equal(4, createdTables.Count);

        var expectedTables = new[] { "corch_edges_raw.contract_creation", "corch_edges_raw.contract_current", "corch_edges_raw.contract_renewal", "corch_edges_raw.contract_termination" };
        var actualMappedNames = createdTables.Select(t => t.mappedName).ToArray();

        foreach (var expectedTable in expectedTables)
        {
            Assert.Contains(expectedTable, actualMappedNames);

            var tableInfo = createdTables.First(t => t.mappedName == expectedTable);
            var rowCount = await GetTableRowCount(tableInfo.qualifiedName);
            Assert.True(rowCount > 0, $"Table {expectedTable} should have data");
        }
    }

    #region Helper Methods

    private async Task<List<(string originalName, string mappedName, string qualifiedName)>> CreateDatabaseTablesFromDataSet(DataSet dataSet)
    {
        var createdTables = new List<(string, string, string)>();

        foreach (DataTable table in dataSet.Tables)
        {
            if (table.Rows.Count == 0) continue;

            var mappedTableName = table.TableName; // Already mapped by adapter
            var tableDefinition = DatabaseTestHelper.GetTableDefinition(table);
            var qualifiedTableName = await SetupTestTable(mappedTableName, tableDefinition);

            // Update table name to qualified name for database operations
            var originalTableName = table.TableName;
            table.TableName = qualifiedTableName;

            createdTables.Add((originalTableName, mappedTableName, qualifiedTableName));
        }

        return createdTables;
    }

    private DataSet CreateTestDataSetWithAllContractTypes()
    {
        var dataSet = new DataSet();
        var contractTypes = new[]
        {
            ("新規to業務管理", "contract_creation"),
            ("契約一覧to業務管理", "contract_current"),
            ("更新to業務管理", "contract_renewal"),
            ("解約to業務管理", "contract_termination")
        };

        foreach (var (sheetName, tableType) in contractTypes)
        {
            var table = CreateTestTableForContractType(sheetName, tableType);
            dataSet.Tables.Add(table);
        }

        return dataSet;
    }

    private DataTable CreateTestTableForContractType(string originalSheetName, string tableType)
    {
        var table = new DataTable(originalSheetName);

        // Add common columns
        table.Columns.Add("物件名", typeof(string));

        // Add type-specific columns
        switch (tableType)
        {
            case "contract_creation":
                table.Columns.Add("契約者名", typeof(string));
                table.Columns.Add("契約日", typeof(DateTime));
                table.Columns.Add("入居予定日", typeof(DateTime));
                table.Columns.Add("礼金(家)", typeof(decimal));
                break;
            case "contract_current":
                table.Columns.Add("契約ID", typeof(string));
                table.Columns.Add("契約者_名", typeof(string));
                table.Columns.Add("契約の状態", typeof(string));
                table.Columns.Add("家賃", typeof(decimal));
                break;
            case "contract_renewal":
                table.Columns.Add("契約ID", typeof(string));
                table.Columns.Add("契約者_名", typeof(string));
                table.Columns.Add("更新日", typeof(DateTime));
                table.Columns.Add("進捗管理ステータス", typeof(string));
                break;
            case "contract_termination":
                table.Columns.Add("契約ID", typeof(string));
                table.Columns.Add("契約者_名", typeof(string));
                table.Columns.Add("_転出日", typeof(DateTime));
                table.Columns.Add("転出点検者", typeof(string));
                break;
        }

        // Add sample data
        var row = table.NewRow();
        row["物件名"] = $"テスト物件_{tableType}";

        // Add type-specific data
        switch (tableType)
        {
            case "contract_creation":
                row["契約者名"] = "田中太郎";
                row["入居予定日"] = DateTime.Now.AddDays(7);
                row["礼金(家)"] = 300000m;
                row["契約日"] = DateTime.Now.AddDays(-30);
                break;
            case "contract_current":
                row["契約ID"] = $"CNT_{tableType.ToUpper()}_001";
                row["契約者_名"] = "田中太郎";
                row["契約の状態"] = "有効";
                row["家賃"] = 100000m;
                break;
            case "contract_renewal":
                row["契約ID"] = $"CNT_{tableType.ToUpper()}_001";
                row["契約者_名"] = "田中太郎";
                row["更新日"] = DateTime.Now.AddDays(30);
                row["進捗管理ステータス"] = "未確認";
                break;
            case "contract_termination":
                row["契約ID"] = $"CNT_{tableType.ToUpper()}_001";
                row["契約者_名"] = "田中太郎";
                row["_転出日"] = DateTime.Now.AddDays(30);
                row["転出点検者"] = "転居";
                break;
        }

        table.Rows.Add(row);
        return table;
    }

    private List<object[]> ExtractRowData(DataTable table)
    {
        var rows = new List<object[]>();
        foreach (DataRow row in table.Rows)
        {
            rows.Add(row.ItemArray!);
        }
        return rows;
    }

    private void VerifyDataIntegrity(List<object[]> originalRows, List<Dictionary<string, object>> insertedData, DataTable sourceTable)
    {
        var sampleSize = Math.Min(3, originalRows.Count);
        for (int i = 0; i < sampleSize; i++)
        {
            var originalRow = originalRows[i];
            var insertedRow = insertedData[i];

            Assert.Equal(originalRow.Length, insertedRow.Count);

            for (int j = 0; j < originalRow.Length; j++)
            {
                var originalValue = originalRow[j];
                var columnName = sourceTable.Columns[j].ColumnName;
                var insertedValue = insertedRow[columnName];

                if (originalValue != null && originalValue != DBNull.Value)
                {
                    Assert.NotNull(insertedValue);
                        
                    // Normalize values for comparison
                    var normalizedOriginal = NormalizeValueForComparison(originalValue);
                    var normalizedInserted = NormalizeValueForComparison(insertedValue);
                        
                    Assert.Equal(normalizedOriginal, normalizedInserted);
                }
            }
        }
    }

    private static string NormalizeValueForComparison(object value)
    {
        if (value == null || value == DBNull.Value) 
            return string.Empty;

        var stringValue = value.ToString();
        if (string.IsNullOrEmpty(stringValue)) 
            return string.Empty;

        // Handle DateTime values - normalize to date-only format if time is 00:00:00
        if (DateTime.TryParse(stringValue, out var dateValue))
        {
            // If time component is midnight, return date-only format
            if (dateValue.TimeOfDay == TimeSpan.Zero)
            {
                return dateValue.ToString("yyyy/MM/dd");
            }
            else
            {
                return dateValue.ToString("yyyy/MM/dd HH:mm:ss");
            }
        }

        // Handle numeric values - remove trailing zeros from decimals
        if (decimal.TryParse(stringValue, out var decimalValue))
        {
            // Remove trailing zeros and unnecessary decimal point
            return decimalValue.ToString("G29"); // G29 format removes trailing zeros
        }

        // Handle other numeric types
        if (double.TryParse(stringValue, out var doubleValue))
        {
            return doubleValue.ToString("G15"); // G15 format removes trailing zeros
        }

        if (float.TryParse(stringValue, out var floatValue))
        {
            return floatValue.ToString("G7"); // G7 format removes trailing zeros
        }

        return stringValue;
    }

    #endregion
}