using System.Data;
using CorchEdges.Data.Abstractions;
using CorchEdges.Data.Mappers;
using CorchEdges.Data.Normalizers;
using CorchEdges.Data.Providers;

namespace CorchEdges.Tests.Unit.Data.Normalizers;

[Trait("Category", "Unit")]
public class DataNormalizerUnitTests
{
    private readonly IDataNormalizer _dataNormalizer;

    public DataNormalizerUnitTests()
    {
        // Create a custom adapter with mock entities for testing functionality
        var customColumnMappings = new Dictionary<string, Dictionary<string, string>>
        {
            {
                "CustomSheet", new Dictionary<string, string>
                {
                    { "OriginalId", "Id" },
                    { "OriginalName", "Name" },
                    { "OriginalIsActive", "IsActive" },
                    { "OriginalIsDeleted", "IsDeleted" }
                }
            },
            {
                "AnotherSheet", new Dictionary<string, string>
                {
                    { "OriginalId", "Id" },
                    { "OriginalTitle", "Title" },
                    { "OriginalAmount", "Amount" },
                    { "OriginalCreatedDate", "CreatedDate" },
                    { "OriginalDueDate", "DueDate" }
                }
            },
            {
                "TestData", new Dictionary<string, string>
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

        var entityDataNormalizer = CreateEntityDataNormalizer(customColumnMappings, customEntityTypeMappings);
        _dataNormalizer = entityDataNormalizer;
    }

    private static IDataNormalizer CreateEntityDataNormalizer(
        Dictionary<string, Dictionary<string, string>> customColumnMappings,
        Dictionary<string, Type> customEntityTypeMappings)
    {
        var metadataProvider = new ReflectionEntityMetadataProvider(customEntityTypeMappings);
        var columnMapper = new EntityBasedColumnMapper(customColumnMappings);
        var entityDataNormalizer = new EntityDataNormalizer(metadataProvider, columnMapper);
        return entityDataNormalizer;
    }

    [Fact]
    public void NormalizeTableTypes_WithTypeConversion_ConvertsDataCorrectly()
    {
        // Arrange
        var sourceTable = new DataTable("CustomSheet");
        sourceTable.Columns.Add("OriginalId", typeof(string));
        sourceTable.Columns.Add("OriginalName", typeof(string));
        sourceTable.Columns.Add("OriginalIsActive", typeof(string));

        var row = sourceTable.NewRow();
        row["OriginalId"] = "42";
        row["OriginalName"] = "Test Entity";
        row["OriginalIsActive"] = "true";
        sourceTable.Rows.Add(row);

        // Act
        var result = _dataNormalizer.NormalizeTypes("custom_table", sourceTable);

        // Assert
        Assert.Equal("custom_table", result.TableName);
        Assert.Single(result.Rows);

        var resultRow = result.Rows[0];
        Assert.Equal(42, resultRow["Id"]);
        Assert.Equal("Test Entity", resultRow["Name"]);
        Assert.Equal(true, resultRow["IsActive"]);
    }

    [Theory]
    [InlineData("123", "Id", typeof(int), 123)]
    [InlineData("Test Name", "Name", typeof(string), "Test Name")]
    [InlineData("true", "IsActive", typeof(bool), true)]
    [InlineData("false", "IsActive", typeof(bool), false)]
    [InlineData("1", "IsActive", typeof(bool), true)]
    [InlineData("0", "IsActive", typeof(bool), false)]
    public void ConvertValueToType_WithVariousTypes_ConvertsCorrectly(string input, string columnName, Type expectedType, object expected)
    {
        // This tests the internal conversion logic used by NormalizeTableTypes
        // We test it indirectly through a table conversion
        
        // Arrange
        var sourceTable = new DataTable("CustomSheet");
        var sourceColumnName = $"Original{columnName}";
        sourceTable.Columns.Add(sourceColumnName, typeof(string));
        
        var row = sourceTable.NewRow();
        row[sourceColumnName] = input;
        sourceTable.Rows.Add(row);

        // Act
        var result = _dataNormalizer.NormalizeTypes("custom_table", sourceTable);

        // Assert
        Assert.Equal(expectedType, result.Columns[columnName]!.DataType);
        Assert.Equal(expected, result.Rows[0][columnName]);
    }

    [Fact]
    public void ConvertValueToType_WithDateTime_ConvertsCorrectly()
    {
        // Test DateTime conversion separately since it needs a different mock entity
        
        // Arrange - Create a custom adapter that maps to an entity with DateTime property
        var customEntityTypeMappings = new Dictionary<string, Type>
        {
            { "datetime_table", typeof(MockComplexEntity) } // Has CreatedDate property
        };
        
        var columnMappings = new Dictionary<string, Dictionary<string, string>>
        {
            {
                "DateTimeSheet", new Dictionary<string, string>
                {
                    { "CreatedDate", "CreatedDate" }
                }
            }
        };
        
        var dataNormalizer = CreateEntityDataNormalizer(columnMappings, customEntityTypeMappings);
        
        var sourceTable = new DataTable("DateTimeSheet");
        sourceTable.Columns.Add("CreatedDate", typeof(string));
        
        var row = sourceTable.NewRow();
        row["CreatedDate"] = "2024-01-01T10:00:00";
        sourceTable.Rows.Add(row);

        // Act
        var result = dataNormalizer.NormalizeTypes("datetime_table", sourceTable);

        // Assert
        Assert.Equal(typeof(DateTime), result.Columns["CreatedDate"]!.DataType);
        Assert.Equal(DateTime.Parse("2024-01-01T10:00:00"), result.Rows[0]["CreatedDate"]);
    }

    [Fact]
    public void ConvertValueToType_WithDecimal_ConvertsCorrectly()
    {
        // Test Decimal conversion with MockComplexEntity.Amount property
        
        // Arrange
        var customEntityTypeMappings = new Dictionary<string, Type>
        {
            { "decimal_table", typeof(MockComplexEntity) }
        };
        
        var columnMappings = new Dictionary<string, Dictionary<string, string>>
        {
            {
                "DecimalSheet", new Dictionary<string, string>
                {
                    { "Amount", "Amount" }
                }
            }
        };

        var dataNormalizer = CreateEntityDataNormalizer(columnMappings, customEntityTypeMappings);
        
        var sourceTable = new DataTable("DecimalSheet");
        sourceTable.Columns.Add("Amount", typeof(string));
        
        var row = sourceTable.NewRow();
        row["Amount"] = "123.45";
        sourceTable.Rows.Add(row);

        // Act
        var result = dataNormalizer.NormalizeTypes("decimal_table", sourceTable);

        // Assert
        Assert.Equal(typeof(decimal), result.Columns["Amount"]!.DataType);
        Assert.Equal(123.45m, result.Rows[0]["Amount"]);
    }

    [Fact]
    public void NormalizeTableTypes_WithNullValues_HandlesNullsCorrectly()
    {
        // Arrange
        var sourceTable = new DataTable("CustomSheet");
        sourceTable.Columns.Add("OriginalId", typeof(string));
        sourceTable.Columns.Add("OriginalName", typeof(string));
        sourceTable.Columns.Add("OriginalIsActive", typeof(string));
        sourceTable.Columns.Add("OriginalIsDeleted", typeof(string)); // Nullable property

        var row = sourceTable.NewRow();
        row["OriginalId"] = "1";
        row["OriginalName"] = DBNull.Value; // Null value
        row["OriginalIsActive"] = ""; // Empty string
        row["OriginalIsDeleted"] = DBNull.Value; // Null value
        sourceTable.Rows.Add(row);

        // Act
        var result = _dataNormalizer.NormalizeTypes("custom_table", sourceTable);

        // Assert
        var resultRow = result.Rows[0];
        Assert.Equal(1, resultRow["Id"]);
        Assert.Equal(DBNull.Value, resultRow["Name"]);
        Assert.Equal(false, resultRow["IsActive"]); // Empty string should become DBNull
        Assert.Equal(DBNull.Value, resultRow["IsDeleted"]); // Null value should stay null
    }

    #region Mock Entities - Same as ExcelToDatabaseConverterUnitTests

    private class MockEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool? IsDeleted { get; set; }
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
}