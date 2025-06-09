// tests/CorchEdges.Tests/Unit/Helpers/DatabaseTestHelperTests.cs

using System.Data;
using CorchEdges.Tests.Helpers;
using Xunit;

namespace CorchEdges.Tests.Unit.Helpers;

[Trait("Category", "Unit")]
[Trait("Component", "DatabaseTestHelper")]
public class DatabaseTestHelperTests
{
    [Fact]
    public void GetTableDefinition_WithVariousDataTypes_ReturnsCorrectPostgresDefinition()
    {
        // Arrange
        var table = new DataTable("test_table");
        table.Columns.Add("id", typeof(int));
        table.Columns.Add("name", typeof(string));
        table.Columns.Add("price", typeof(decimal));
        table.Columns.Add("is_active", typeof(bool));
        table.Columns.Add("created_date", typeof(DateTime));

        // Act
        var definition = DatabaseTestHelper.GetTableDefinition(table);

        // Assert
        Assert.Contains("\"id\" INTEGER", definition);
        Assert.Contains("\"name\" TEXT", definition);
        Assert.Contains("\"price\" DECIMAL(18,2)", definition);
        Assert.Contains("\"is_active\" BOOLEAN", definition);
        Assert.Contains("\"created_date\" TIMESTAMP", definition);
    }

    [Fact]
    public void GetTableDefinition_WithSpecialCharactersInColumnNames_QuotesColumnNames()
    {
        // Arrange
        var table = new DataTable("test_table");
        table.Columns.Add("契約ID", typeof(string));
        table.Columns.Add("物件名", typeof(string));
        table.Columns.Add("column with spaces", typeof(string));

        // Act
        var definition = DatabaseTestHelper.GetTableDefinition(table);

        // Assert
        Assert.Contains("\"契約ID\" TEXT", definition);
        Assert.Contains("\"物件名\" TEXT", definition);
        Assert.Contains("\"column with spaces\" TEXT", definition);
    }

    [Fact]
    public void GetTableDefinition_WithEmptyTable_ReturnsEmptyString()
    {
        // Arrange
        var table = new DataTable("empty_table");

        // Act
        var definition = DatabaseTestHelper.GetTableDefinition(table);

        // Assert
        Assert.Equal(string.Empty, definition);
    }

    [Fact]
    public void GetTableDefinition_WithSingleColumn_ReturnsCorrectDefinition()
    {
        // Arrange
        var table = new DataTable("single_column_table");
        table.Columns.Add("only_column", typeof(long));

        // Act
        var definition = DatabaseTestHelper.GetTableDefinition(table);

        // Assert
        Assert.Equal("\"only_column\" BIGINT", definition);
    }

    [Theory]
    [InlineData(typeof(string), "TEXT")]
    [InlineData(typeof(int), "INTEGER")]
    [InlineData(typeof(long), "BIGINT")]
    [InlineData(typeof(decimal), "DECIMAL(18,2)")]
    [InlineData(typeof(double), "DOUBLE PRECISION")]
    [InlineData(typeof(bool), "BOOLEAN")]
    [InlineData(typeof(DateTime), "TIMESTAMP")]
    [InlineData(typeof(DateOnly), "DATE")]
    public void GetTableDefinition_WithSpecificDataType_ReturnsCorrectPostgresType(Type dotNetType, string expectedPostgresType)
    {
        // Arrange
        var table = new DataTable("type_test_table");
        table.Columns.Add("test_column", dotNetType);

        // Act
        var definition = DatabaseTestHelper.GetTableDefinition(table);

        // Assert
        Assert.Equal($"\"test_column\" {expectedPostgresType}", definition);
    }

    [Fact]
    public void GetTableDefinition_WithUnknownType_DefaultsToText()
    {
        // Arrange
        var table = new DataTable("unknown_type_table");
        table.Columns.Add("unknown_column", typeof(Guid)); // Guid should default to TEXT

        // Act
        var definition = DatabaseTestHelper.GetTableDefinition(table);

        // Assert
        Assert.Equal("\"unknown_column\" TEXT", definition);
    }

    [Fact]
    public void GetTableDefinition_WithMultipleColumns_ReturnsCommaSeparatedDefinition()
    {
        // Arrange
        var table = new DataTable("multi_column_table");
        table.Columns.Add("col1", typeof(int));
        table.Columns.Add("col2", typeof(string));
        table.Columns.Add("col3", typeof(bool));

        // Act
        var definition = DatabaseTestHelper.GetTableDefinition(table);

        // Assert
        Assert.Equal("\"col1\" INTEGER, \"col2\" TEXT, \"col3\" BOOLEAN", definition);
    }
}