// tests/CorchEdges.Tests/Unit/Data/ExcelToDatabaseConverterUnitTests.cs

using System.Data;
using CorchEdges.Data;
using CorchEdges.Data.Abstractions;
using Xunit;

namespace CorchEdges.Tests.Unit.Data;

[Trait("Category", "Unit")]
[Trait("Component", "ExcelToDatabaseConverter")]
public class ExcelToDatabaseConverterUnitTests
{
    private readonly  IDataSetConverter _dataSetConverter;

    public ExcelToDatabaseConverterUnitTests()
    {
        // Create a custom adapter with mock entities for testing functionality
        var customTableMappings = new Dictionary<string, string>
        {
            { "CustomSheet", "custom_table" },
            { "AnotherSheet", "another_table" },
            { "TestData", "test_data_table" }
        };

        var customColumnMappings = new Dictionary<string, Dictionary<string, string>>
        {
            {
                "CustomSheet", new Dictionary<string, string>  // ✅ Correct - source table name
                {
                    { "OriginalId", "Id" },
                    { "OriginalName", "Name" },
                    { "OriginalIsActive", "IsActive" },
                    { "OriginalIsDeleted", "IsDeleted" }
                }
            },
            {
                "AnotherSheet", new Dictionary<string, string>  // ✅ Correct - source table name
                {
                    { "OriginalId", "Id" },
                    { "OriginalTitle", "Title" },
                    { "OriginalAmount", "Amount" },
                    { "OriginalCreatedDate", "CreatedDate" },
                    { "OriginalDueDate", "DueDate" }
                }
            },
            {
                "TestData", new Dictionary<string, string>  // ✅ Correct - source table name
                {
                    { "OriginalCode", "Code" },
                    { "OriginalDescription", "Description" }
                }
            }
        };

        // Mock entity types for testing
        var customEntityTypeMappings = new Dictionary<string, Type>
        {
            { "custom_table", typeof(MockEntity) },
            { "another_table", typeof(MockComplexEntity) },
            { "test_data_table", typeof(MockSimpleEntity) }
        };

        _dataSetConverter = new ExcelToDatabaseConverter(customEntityTypeMappings, customTableMappings, customColumnMappings);
    }



    [Fact]
    public void PrepareDataSetForDatabase_WithCustomMappings_ProcessesCorrectly()
    {
        // Arrange
        var dataSet = new DataSet();
        var table = new DataTable("CustomSheet");
        table.Columns.Add("OriginalId", typeof(string));
        table.Columns.Add("OriginalName", typeof(string));
        table.Columns.Add("OriginalIsActive", typeof(string));

        var row = table.NewRow();
        row["OriginalId"] = "1";
        row["OriginalName"] = "Test Name";
        row["OriginalIsActive"] = "true";
        table.Rows.Add(row);

        dataSet.Tables.Add(table);

        // Act
        var result = _dataSetConverter.ConvertForDatabase(dataSet);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Tables);

        var resultTable = result.Tables[0];
        Assert.Equal("custom_table", resultTable.TableName);
        Assert.Equal(typeof(int), resultTable.Columns["Id"]!.DataType);
        Assert.Equal(typeof(string), resultTable.Columns["Name"]!.DataType);
        Assert.Equal(typeof(bool), resultTable.Columns["IsActive"]!.DataType);
    }


    #region Mock Entities for Testing

    private class MockEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool? IsDeleted { get; set; } // Nullable property
    }

    private class MockComplexEntity
    {
        public long Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateOnly? DueDate { get; set; }
    }

    private class MockSimpleEntity
    {
        public string Code { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    #endregion



    [Fact]
    public void PrepareDataSetForDatabase_WithEmptyTables_SkipsEmptyTables()
    {
        // Arrange
        var dataSet = new DataSet();
            
        // Add table with data
        var tableWithData = new DataTable("CustomSheet");
        tableWithData.Columns.Add("OriginalId", typeof(string));
        tableWithData.Columns.Add("OriginalName", typeof(string));
        tableWithData.Columns.Add("OriginalIsActive", typeof(string));
        var row = tableWithData.NewRow();
        row["OriginalId"] = "1";
        row["OriginalName"] = "Test";
        row["OriginalIsActive"] = "true";
        tableWithData.Rows.Add(row);
            
        // Add empty table
        var emptyTable = new DataTable("AnotherSheet");
        emptyTable.Columns.Add("Id", typeof(string));
        emptyTable.Columns.Add("Title", typeof(string));
        // No rows added
            
        dataSet.Tables.Add(tableWithData);
        dataSet.Tables.Add(emptyTable);

        // Act
        var result = _dataSetConverter.ConvertForDatabase(dataSet);

        // Assert
        // Should only process tables with data
        Assert.Single(result.Tables);
        Assert.Equal("custom_table", result.Tables[0].TableName);
    }
}