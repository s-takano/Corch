// tests/CorchEdges.Tests/Integration/Data/ExcelToDatabaseConverterIntegrationTests.cs

using System.Data;
using CorchEdges.Data;
using CorchEdges.Data.Abstractions;
using CorchEdges.Data.Entities;
using Xunit;

namespace CorchEdges.Tests.Integration.Data;

[Trait("Category", "Integration")]
[Trait("Component", "Data")]
[Trait("Target", "ExcelToDatabaseConverter")]
public class ExcelToMemoryDatabaseConverterIntegrationTests : MemoryDatabaseTestBase
{
    private readonly IDataSetConverter _dataSetConverter = new ExcelToDatabaseConverter();

    [Fact]
    public void PrepareDataSetForDatabase_WithRealContractData_ProcessesSuccessfully()
    {
        // Arrange
        var sourceDataSet = CreateRealContractDataSet();

        // Act
        var result = _dataSetConverter.ConvertForDatabase(sourceDataSet);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Tables);
            
        var table = result.Tables[0];
        Assert.Equal("corch_edges_raw.contract_creation", table.TableName);
            
        // Verify DataTable column types (underlying types, not nullable)
        Assert.Equal(typeof(string), table.Columns["契約ID"]!.DataType);
        Assert.Equal(typeof(int), table.Columns["物件No"]!.DataType); // int, not int?
        Assert.Equal(typeof(string), table.Columns["物件名"]!.DataType);
        Assert.Equal(typeof(DateTime), table.Columns["出力日時"]!.DataType); // DateTime, not DateTime?
            
        // Verify nullable columns allow DBNull
        Assert.True(table.Columns["物件No"]!.AllowDBNull);
        Assert.True(table.Columns["出力日時"]!.AllowDBNull);
    }

    [Fact]
    public void NormalizeTableTypes_WithNullableTypes_HandlesNullsCorrectly()
    {
        // Test that nullable entity properties are handled correctly
            
        // Arrange
        var sourceTable = new DataTable("新規to業務管理");
        sourceTable.Columns.Add("契約ID", typeof(string));
        sourceTable.Columns.Add("物件No", typeof(string));
        sourceTable.Columns.Add("出力日時", typeof(string));

        // Add row with null values for nullable columns
        var row1 = sourceTable.NewRow();
        row1["契約ID"] = "CONTRACT_001";
        row1["物件No"] = DBNull.Value; // Null for nullable int
        row1["出力日時"] = DBNull.Value; // Null for nullable DateTime
        sourceTable.Rows.Add(row1);
            
        // Add row with values
        var row2 = sourceTable.NewRow();
        row2["契約ID"] = "CONTRACT_002";
        row2["物件No"] = "456";
        row2["出力日時"] = "2024-01-01T10:00:00";
        sourceTable.Rows.Add(row2);

        var dataSet = new DataSet();
        dataSet.Tables.Add(sourceTable);
            
        // Act
        var result = _dataSetConverter.ConvertForDatabase(dataSet);

        // Assert
        Assert.Equal(2, result.Tables[0].Rows.Count);
            
        // First row - with nulls
        var resultRow1 = result.Tables[0].Rows[0];
        Assert.Equal("CONTRACT_001", resultRow1["契約ID"]);
        Assert.Equal(DBNull.Value, resultRow1["物件No"]);
        Assert.Equal(DBNull.Value, resultRow1["出力日時"]);
            
        // Second row - with values
        var resultRow2 = result.Tables[0].Rows[1];
        Assert.Equal("CONTRACT_002", resultRow2["契約ID"]);
        Assert.Equal(456, resultRow2["物件No"]);
        Assert.Equal(DateTime.Parse("2024-01-01T10:00:00"), resultRow2["出力日時"]);
            
        // Verify column settings for nullable behavior
        Assert.True(result.Tables[0].Columns["物件No"]!.AllowDBNull, "int? property allows nulls");
        Assert.True(result.Tables[0].Columns["出力日時"]!.AllowDBNull, "DateTime? property allows nulls");
        Assert.True(result.Tables[0].Columns["契約ID"]!.AllowDBNull, "string? property allows nulls (reference type)");
    }

    private DataSet CreateRealContractDataSet()
    {
        var dataSet = new DataSet();
        var table = new DataTable("新規to業務管理");
            
        // Add columns that match real ContractCreation entity
        table.Columns.Add("契約ID", typeof(string));
        table.Columns.Add("物件No", typeof(string)); // Will be converted to int?
        table.Columns.Add("物件名", typeof(string));
        table.Columns.Add("出力日時", typeof(string)); // Will be converted to DateTime

        // Add sample data
        var row = table.NewRow();
        row["契約ID"] = "CONTRACT_001";
        row["物件No"] = "123";
        row["物件名"] = "Test Property";
        row["出力日時"] = "2024-01-01T10:00:00";
        table.Rows.Add(row);

        dataSet.Tables.Add(table);
        return dataSet;
    }
    [Fact]
    public void PrepareDataSetForDatabase_WithUnknownTable_ThrowsException()
    {
        // Arrange
        var dataSet = new DataSet();
        var unknownTable = new DataTable("UnknownTableName");
        unknownTable.Columns.Add("SomeColumn", typeof(string));
    
        var row = unknownTable.NewRow();
        row["SomeColumn"] = "test";
        unknownTable.Rows.Add(row);
        dataSet.Tables.Add(unknownTable);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            _dataSetConverter.ConvertForDatabase(dataSet));
    
        Assert.Contains("Invalid table name", exception.Message);
    }
    [Fact]
    public void PrepareDataSetForDatabase_NormalizesColumnTypes()
    {
        // Arrange
        var dataSet = CreateRealContractDataSet();

        // Act
        var result = _dataSetConverter.ConvertForDatabase(dataSet);

        // Assert
        var resultTable = result.Tables[0];

        // Verify column types are normalized to entity types
        Assert.Equal(typeof(string), resultTable.Columns["契約ID"]!.DataType);
        Assert.Equal(typeof(int), resultTable.Columns["物件No"]!.DataType);
        Assert.Equal(typeof(DateTime), resultTable.Columns["出力日時"]!.DataType);
    }
}