// tests/CorchEdges.Tests/Unit/Data/ExcelToDatabaseAdapterUnitTests.cs

using System.Data;
using CorchEdges.Data;
using Xunit;

namespace CorchEdges.Tests.Unit.Data
{
    [Trait("Category", "Unit")]
    [Trait("Component", "ExcelToDatabaseAdapter")]
    public class ExcelToDatabaseAdapterUnitTests
    {
        private readonly IExcelToDatabaseAdapter _customAdapter;

        public ExcelToDatabaseAdapterUnitTests()
        {
            // Create custom adapter with mock entities for testing functionality
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

            _customAdapter =
                new ExcelToDatabaseAdapter(customEntityTypeMappings, customTableMappings, customColumnMappings);
        }


        [Fact]
        public void GetColumnTypeFromEntity_WithMockEntity_ReturnsCorrectTypes()
        {
            // Act & Assert
            Assert.Equal(typeof(int), _customAdapter.GetColumnTypeFromEntity("custom_table", "Id"));
            Assert.Equal(typeof(string), _customAdapter.GetColumnTypeFromEntity("custom_table", "Name"));
            Assert.Equal(typeof(bool), _customAdapter.GetColumnTypeFromEntity("custom_table", "IsActive"));
        }

        [Fact]
        public void GetColumnTypeFromEntity_WithInvalidColumn_ThrowsInvalidOperationException()
        {
            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() =>
                _customAdapter.GetColumnTypeFromEntity("custom_table", "NonExistentColumn"));

            Assert.Contains("Column 'NonExistentColumn' not found", exception.Message);
            Assert.Contains("Available properties: Id, Name, IsActive", exception.Message);
        }

        [Fact]
        public void GetColumnTypeFromEntity_WithInvalidTable_ThrowsArgumentException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                _customAdapter.GetColumnTypeFromEntity("invalid_table", "SomeColumn"));

            Assert.Contains("No entity mapping found for table 'invalid_table'", exception.Message);
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
            var result = _customAdapter.ConvertDataSetForDatabase(dataSet);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result.Tables);

            var resultTable = result.Tables[0];
            Assert.Equal("custom_table", resultTable.TableName);
            Assert.Equal(typeof(int), resultTable.Columns["Id"]!.DataType);
            Assert.Equal(typeof(string), resultTable.Columns["Name"]!.DataType);
            Assert.Equal(typeof(bool), resultTable.Columns["IsActive"]!.DataType);
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
            var result = _customAdapter.NormalizeTableTypes("custom_table", sourceTable);

            // Assert
            Assert.Equal("custom_table", result.TableName);
            Assert.Single(result.Rows);

            var resultRow = result.Rows[0];
            Assert.Equal(42, resultRow["Id"]);
            Assert.Equal("Test Entity", resultRow["Name"]);
            Assert.Equal(true, resultRow["IsActive"]);
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
            var result = _customAdapter.NormalizeTableTypes("custom_table", sourceTable);

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
            
            var customTableMappings = new Dictionary<string, string>
            {
                { "DateTimeSheet", "datetime_table" }
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
            
            var adapter = new ExcelToDatabaseAdapter(customEntityTypeMappings, customTableMappings, columnMappings);
            
            var sourceTable = new DataTable("DateTimeSheet");
            sourceTable.Columns.Add("CreatedDate", typeof(string));
            
            var row = sourceTable.NewRow();
            row["CreatedDate"] = "2024-01-01T10:00:00";
            sourceTable.Rows.Add(row);

            // Act
            var result = adapter.NormalizeTableTypes("datetime_table", sourceTable);

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
            
            var customTableMappings = new Dictionary<string, string>
            {
                { "DecimalSheet", "decimal_table" }
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

            var adapter = new ExcelToDatabaseAdapter(customEntityTypeMappings, customTableMappings, columnMappings);
            
            var sourceTable = new DataTable("DecimalSheet");
            sourceTable.Columns.Add("Amount", typeof(string));
            
            var row = sourceTable.NewRow();
            row["Amount"] = "123.45";
            sourceTable.Rows.Add(row);

            // Act
            var result = adapter.NormalizeTableTypes("decimal_table", sourceTable);

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
            var result = _customAdapter.NormalizeTableTypes("custom_table", sourceTable);

            // Assert
            var resultRow = result.Rows[0];
            Assert.Equal(1, resultRow["Id"]);
            Assert.Equal(DBNull.Value, resultRow["Name"]);
            Assert.Equal(false, resultRow["IsActive"]); // Empty string should become DBNull
            Assert.Equal(DBNull.Value, resultRow["IsDeleted"]); // Null value should stay null
        }

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
            var result = _customAdapter.ConvertDataSetForDatabase(dataSet);

            // Assert
            // Should only process tables with data
            Assert.Single(result.Tables);
            Assert.Equal("custom_table", result.Tables[0].TableName);
        }
    }
}