using System.Data;
using CorchEdges.Abstractions;
using CorchEdges.Data;
using CorchEdges.Data.Abstractions;
using CorchEdges.Tests.Infrastructure;
using CorchEdges.Utilities;

namespace CorchEdges.Tests.Integration.Data;

[Trait("Category", TestCategories.Integration)]
public class StrictSchemaDetectorIntegrationTests
{
    private readonly ITabularDataParser _parser = new ExcelDataParser();
    private readonly IDataSetConverter _converter = new ExcelToDatabaseConverter();

    [Fact]
    public void ConvertForDatabase_WithValidExcelFile_ShouldCorrectlyDetectAndMapAllSchemas()
    {
        // Arrange: Load the real sample Excel file used in production/UAT
        var excelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Files", "valid-data.xlsx");
        if (!File.Exists(excelPath))
        {
            throw new FileNotFoundException($"Test Excel file not found at: {excelPath}");
        }

        using var stream = File.OpenRead(excelPath);
        var (sourceDataSet, _) = _parser.Parse(stream);

        // Act: This triggers the StrictSchemaDetector internally
        var result = _converter.ConvertForDatabase(sourceDataSet!);

        // Assert: Verify that the sheets were mapped to the expected database tables
        // If the detector fails, it throws an ArgumentException before reaching here.
        
        var tableNames = result.Tables.Cast<DataTable>().Select(t => t.TableName).ToList();

        // Check for Contract Creation (新規to業務管理)
        Assert.Contains("corch_edges_raw.contract_creation", tableNames);
        
        // Check for Contract Renewal (更新to業務管理)
        Assert.Contains("corch_edges_raw.contract_renewal", tableNames);
        
        // Check for Contract Current (契約一覧to業務管理)
        Assert.Contains("corch_edges_raw.contract_current", tableNames);

        // Verify that specific columns were correctly typed by the configuration
        var creationTable = result.Tables["corch_edges_raw.contract_creation"];
        Assert.Equal(typeof(int), creationTable!.Columns["物件No"]!.DataType);
        Assert.Equal(typeof(DateTime), creationTable.Columns["出力日時"]!.DataType);
    }

    [Fact]
    public void Detector_WithMismatchedSheetName_ShouldThrowArgumentException()
    {
        // Arrange: Create a dataset with a sheet name that doesn't exist in any configuration
        var dataSet = new DataSet();
        var invalidTable = new DataTable("WrongSheetName");
        invalidTable.Columns.Add("契約ID", typeof(string));
        invalidTable.Rows.Add("TEST-001");
        dataSet.Tables.Add(invalidTable);

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _converter.ConvertForDatabase(dataSet));
        Assert.Contains("No strict schema match found for sheet 'WrongSheetName'", ex.Message);
    }
}
