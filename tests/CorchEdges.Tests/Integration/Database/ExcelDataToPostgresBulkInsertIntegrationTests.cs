using System.Data;
using CorchEdges.Data;
using CorchEdges.Data.Abstractions;
using CorchEdges.Data.Configurations;
using CorchEdges.Tests.Helpers;
using CorchEdges.Tests.Infrastructure;
using CorchEdges.Utilities;

namespace CorchEdges.Tests.Integration.Database;

[Trait("Category", TestCategories.Integration)]
[Trait("Target", "PostgresTableWriter")]
[Trait("Requires", InfrastructureRequirements.PostgreSql)]
public class ExcelDataToPostgresBulkInsertIntegrationTests : PostgresDatabaseTestBase
{
    
    private readonly IPostgresTableWriter _writer = new PostgresTableWriter();
    private readonly ExcelDataParser _excelParser = new();
    private readonly IDataSetConverter _dataSetConverter = new ExcelToDatabaseConverter();
        

    [Fact]
    public async Task WriteAsync_FromValidExcelFile_InsertsDataSuccessfully()
    {
        // Arrange
        var excelFilePath = Path.Combine("Data", "Files", "valid-data.xlsx");
        Assert.True(File.Exists(excelFilePath), $"Test file not found: {excelFilePath}");

        var excelBytes = await File.ReadAllBytesAsync(excelFilePath, TestContext.Current.CancellationToken);
        var (sourceDataSet, _) = _excelParser.Parse(new MemoryStream(excelBytes));

        Assert.NotNull(sourceDataSet);
        Assert.True(sourceDataSet.Tables.Count > 0, "Excel file should contain at least one table");

        // Prepare data for database
        var preparedDataSet = _dataSetConverter.ConvertForDatabase(sourceDataSet);
        var createdTables = await CreateDatabaseTablesFromDataSet(preparedDataSet);

        await using var transaction = await Connection.BeginTransactionAsync(TestContext.Current.CancellationToken);

        // Act
        await _writer.WriteAsync(preparedDataSet, Connection, transaction);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

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
    public async Task WriteAsync_FromValidExcelFile_DataIntegrityCheck()
    {
        // Arrange
        var excelFilePath = Path.Combine("Data", "Files", "valid-data.xlsx");
        byte[] excelBytes = await File.ReadAllBytesAsync(excelFilePath, TestContext.Current.CancellationToken);
        var (sourceDataSet, _) = _excelParser.Parse(new MemoryStream(excelBytes));

        var preparedDataSet = _dataSetConverter.ConvertForDatabase(sourceDataSet!);
        var sourceTable = preparedDataSet.Tables.Cast<DataTable>().First(t => t.Rows.Count > 0);

        // Store original data for comparison
        var originalRows = ExtractRowData(sourceTable);

        // Create database table
        var tableDefinition = DatabaseTestHelper.GetTableDefinition(sourceTable);
        var qualifiedTableName = await SetupTestTableFromMappedName(sourceTable.TableName, tableDefinition);
            
        // Create a copy of the table with the qualified name for database operations
        var tableForDatabase = sourceTable.Copy();
        tableForDatabase.TableName = qualifiedTableName;

        await using var transaction = await Connection.BeginTransactionAsync(TestContext.Current.CancellationToken);

        // Act
        var singleTableDataSet = new DataSet();
        singleTableDataSet.Tables.Add(tableForDatabase); // Now using a copy
        await _writer.WriteAsync(singleTableDataSet, Connection, transaction);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        // Assert
        var insertedData = await GetTableData(qualifiedTableName);
        Assert.Equal(originalRows.Count, insertedData.Count);

        VerifyDataIntegrity(originalRows, insertedData, tableForDatabase);
    }

    [Fact]
    public async Task WriteAsync_AllContractSheetsMapping_CreatesCorrectTables()
    {
        // Arrange
        var testDataSet = CreateTestDataSetWithAllContractTypes();
        var preparedDataSet = _dataSetConverter.ConvertForDatabase(testDataSet);
        var createdTables = await CreateDatabaseTablesFromDataSet(preparedDataSet);

        await using var transaction = await Connection.BeginTransactionAsync(TestContext.Current.CancellationToken);

        // Act
        await _writer.WriteAsync(preparedDataSet, Connection, transaction);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

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
            var qualifiedTableName = await SetupTestTableFromMappedName(mappedTableName, tableDefinition);

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
        IEntityTypeMetaInfo config = tableType switch
        {
            "contract_creation" => new ContractCreationConfigurationV1(),
            "contract_current" => new ContractCurrentConfigurationV1(),
            "contract_renewal" => new ContractRenewalConfigurationV1(),
            "contract_termination" => new ContractTerminationConfigurationV1(),
            _ => throw new ArgumentException($"Unknown contract type: {tableType}")
        };

        var table = DataTableTestHelper.CreateDataTableFromConfiguration(config);

        // Add sample data (all values as strings since Excel input is string-based)
        var row = table.NewRow();

        if (table.Columns.Contains("物件名"))
            row["物件名"] = $"テスト物件_{tableType}";

        switch (tableType)
        {
            case "contract_creation":
                if (table.Columns.Contains("契約者名")) row["契約者名"] = "田中太郎";
                if (table.Columns.Contains("入居予定日")) row["入居予定日"] = DateTime.Now.AddDays(7).ToString("yyyy-MM-dd");
                if (table.Columns.Contains("礼金_家")) row["礼金_家"] = "300000";
                if (table.Columns.Contains("契約日")) row["契約日"] = DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd");
                break;

            case "contract_current":
                if (table.Columns.Contains("契約ID")) row["契約ID"] = $"CNT_{tableType.ToUpper()}_001";
                if (table.Columns.Contains("契約者_名")) row["契約者_名"] = "田中太郎";
                if (table.Columns.Contains("契約状態")) row["契約状態"] = "有効";
                if (table.Columns.Contains("家賃")) row["家賃"] = "100000";
                break;

            case "contract_renewal":
                if (table.Columns.Contains("契約ID")) row["契約ID"] = $"CNT_{tableType.ToUpper()}_001";
                if (table.Columns.Contains("契約者_名")) row["契約者_名"] = "田中太郎";
                if (table.Columns.Contains("更新日")) row["更新日"] = DateTime.Now.AddDays(30).ToString("yyyy-MM-dd");
                if (table.Columns.Contains("進捗管理ステータス")) row["進捗管理ステータス"] = "未確認";
                break;

            case "contract_termination":
                if (table.Columns.Contains("契約ID")) row["契約ID"] = $"CNT_{tableType.ToUpper()}_001";
                if (table.Columns.Contains("契約者_名")) row["契約者_名"] = "田中太郎";
                if (table.Columns.Contains("_転出日")) row["_転出日"] = DateTime.Now.AddDays(30).ToString("yyyy-MM-dd");
                if (table.Columns.Contains("転出点検者")) row["転出点検者"] = "転居";
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