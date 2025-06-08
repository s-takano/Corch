
// tests/CorchEdges.Tests/Unit/Data/Providers/ReflectionEntityMetadataProviderUnitTests.cs

using System.Reflection;
using CorchEdges.Data.Providers;
using Xunit;

namespace CorchEdges.Tests.Unit.Data.Providers
{
    [Trait("Category", "Unit")]
    [Trait("Component", "ReflectionEntityMetadataProvider")]
    public class ReflectionEntityMetadataProviderUnitTests
    {
        private readonly ReflectionEntityMetadataProvider _metadataProvider;
        
        public ReflectionEntityMetadataProviderUnitTests()
        {
            var entityTypeMappings = new Dictionary<string, Type>
            {
                { "custom_table", typeof(MockEntity) },
                { "another_table", typeof(MockComplexEntity) },
                { "test_data_table", typeof(MockSimpleEntity) },
                { "nullable_entity", typeof(NullableEntity) }
            };

            _metadataProvider = new ReflectionEntityMetadataProvider(entityTypeMappings);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidMappings_DoesNotThrow()
        {
            // Arrange
            var mappings = new Dictionary<string, Type>
            {
                { "table1", typeof(MockEntity) }
            };

            // Act & Assert - Should not throw
            var provider = new ReflectionEntityMetadataProvider(mappings);
            Assert.NotNull(provider);
        }

        [Fact]
        public void Constructor_WithEmptyMappings_DoesNotThrow()
        {
            // Arrange
            var emptyMappings = new Dictionary<string, Type>();

            // Act & Assert - Should not throw
            var provider = new ReflectionEntityMetadataProvider(emptyMappings);
            Assert.NotNull(provider);
        }

        [Fact]
        public void Constructor_WithNullMappings_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => 
                new ReflectionEntityMetadataProvider(null!));
            Assert.Equal("entityTypes", exception.ParamName);
        }

        #endregion

        #region GetColumnType Tests - Covering ExcelToDatabaseConverterUnitTests scenarios

        [Theory]
        [InlineData("custom_table", "Id", typeof(int))]
        [InlineData("custom_table", "Name", typeof(string))]
        [InlineData("custom_table", "IsActive", typeof(bool))]
        [InlineData("custom_table", "IsDeleted", typeof(bool?))]
        public void GetColumnType_WithMockEntity_ReturnsCorrectTypes(string tableName, string columnName, Type expectedType)
        {
            // This directly mirrors ExcelToDatabaseConverterUnitTests.GetColumnTypeFromEntity_WithMockEntity_ReturnsCorrectTypes
            
            // Act
            var result = _metadataProvider.GetColumnType(tableName, columnName);

            // Assert
            Assert.Equal(expectedType, result);
        }

        [Fact]
        public void GetColumnType_WithInvalidColumn_ThrowsInvalidOperationException()
        {
            // This directly mirrors ExcelToDatabaseConverterUnitTests.GetColumnTypeFromEntity_WithInvalidColumn_ThrowsInvalidOperationException
            
            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() =>
                _metadataProvider.GetColumnType("custom_table", "NonExistentColumn"));

            Assert.Contains("Column 'NonExistentColumn' not found", exception.Message);
            Assert.Contains("Available properties: Id, Name, IsActive, IsDeleted", exception.Message);
        }

        [Fact]
        public void GetColumnType_WithInvalidTable_ThrowsArgumentException()
        {
            // This directly mirrors ExcelToDatabaseConverterUnitTests.GetColumnTypeFromEntity_WithInvalidTable_ThrowsArgumentException
            
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                _metadataProvider.GetColumnType("invalid_table", "SomeColumn"));

            Assert.Contains("No entity mapping found for table 'invalid_table'", exception.Message);
        }

        [Theory]
        [InlineData("another_table", "Id", typeof(long))]
        [InlineData("another_table", "Title", typeof(string))]
        [InlineData("another_table", "Amount", typeof(decimal))]
        [InlineData("another_table", "CreatedDate", typeof(DateTime))]
        [InlineData("another_table", "DueDate", typeof(DateOnly?))]
        public void GetColumnType_WithComplexEntity_ReturnsCorrectTypes(string tableName, string columnName, Type expectedType)
        {
            // Covers the complex entity types used in ExcelToDatabaseConverterUnitTests
            
            // Act
            var result = _metadataProvider.GetColumnType(tableName, columnName);

            // Assert
            Assert.Equal(expectedType, result);
        }

        [Theory]
        [InlineData("test_data_table", "Code", typeof(string))]
        [InlineData("test_data_table", "Description", typeof(string))]
        public void GetColumnType_WithSimpleEntity_ReturnsCorrectTypes(string tableName, string columnName, Type expectedType)
        {
            // Covers the simple entity types used in ExcelToDatabaseConverterUnitTests
            
            // Act
            var result = _metadataProvider.GetColumnType(tableName, columnName);

            // Assert
            Assert.Equal(expectedType, result);
        }

        [Theory]
        [InlineData("nullable_entity", "NullableInt", typeof(int?))]
        [InlineData("nullable_entity", "NullableBool", typeof(bool?))]
        [InlineData("nullable_entity", "NullableDateTime", typeof(DateTime?))]
        [InlineData("nullable_entity", "NullableDateOnly", typeof(DateOnly?))]
        public void GetColumnType_WithNullableTypes_ReturnsNullableType(string tableName, string columnName, Type expectedType)
        {
            // Act
            var result = _metadataProvider.GetColumnType(tableName, columnName);

            // Assert
            Assert.Equal(expectedType, result);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void GetColumnType_WithInvalidColumnName_ThrowsInvalidOperationException(string? columnName)
        {
            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => 
                _metadataProvider.GetColumnType("custom_table", columnName!));
            
            Assert.Contains("not found in entity", exception.Message);
        }

        [Fact]
        public void GetColumnType_CaseSensitive_ThrowsForIncorrectCase()
        {
            // Act & Assert - Property names are case-sensitive
            var exception = Assert.Throws<InvalidOperationException>(() => 
                _metadataProvider.GetColumnType("custom_table", "id")); // lowercase 'id' instead of 'Id'
            
            Assert.Contains("Column 'id' not found", exception.Message);
        }

        #endregion

        #region HasTable Tests

        [Theory]
        [InlineData("custom_table", true)]
        [InlineData("another_table", true)]
        [InlineData("test_data_table", true)]
        [InlineData("nullable_entity", true)]
        public void HasTable_WithValidTableNames_ReturnsTrue(string tableName, bool expected)
        {
            // Act
            var result = _metadataProvider.HasTable(tableName);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("unknown_table")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("non_existent")]
        public void HasTable_WithInvalidTableNames_ReturnsFalse(string tableName)
        {
            // Act
            var result = _metadataProvider.HasTable(tableName);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void HasTable_WithNullTableName_ReturnsFalse()
        {
            // Act
            var result = _metadataProvider.HasTable(null!);

            // Assert
            Assert.False(result);
        }

        #endregion

        #region HasColumn Tests

        [Theory]
        [InlineData("custom_table", "Id", true)]
        [InlineData("custom_table", "Name", true)]
        [InlineData("custom_table", "IsActive", true)]
        [InlineData("custom_table", "IsDeleted", true)]
        public void HasColumn_WithValidColumns_ReturnsTrue(string tableName, string columnName, bool expected)
        {
            // Act
            var result = _metadataProvider.HasColumn(tableName, columnName);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("custom_table", "NonExistentColumn")]
        [InlineData("custom_table", "id")] // wrong case
        [InlineData("custom_table", "")]
        [InlineData("custom_table", "   ")]
        public void HasColumn_WithInvalidColumns_ReturnsFalse(string tableName, string columnName)
        {
            // Act
            var result = _metadataProvider.HasColumn(tableName, columnName);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData("unknown_table", "AnyColumn")]
        [InlineData("", "AnyColumn")]
        [InlineData("   ", "AnyColumn")]
        public void HasColumn_WithInvalidTable_ReturnsFalse(string tableName, string columnName)
        {
            // Act
            var result = _metadataProvider.HasColumn(tableName, columnName);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void HasColumn_WithNullInputs_ReturnsFalse()
        {
            // Act & Assert
            Assert.False(_metadataProvider.HasColumn(null!, "AnyColumn"));
            Assert.False(_metadataProvider.HasColumn("custom_table", null!));
            Assert.False(_metadataProvider.HasColumn(null!, null!));
        }

        #endregion

        #region Reflection Behavior Tests

        [Fact]
        public void GetColumnType_UsesPublicInstanceBindingFlags()
        {
            // This test verifies that only public instance properties are accessible
            // Private, static, or other properties should not be found
            
            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => 
                _metadataProvider.GetColumnType("custom_table", "PrivateProperty"));
            
            Assert.Contains("Column 'PrivateProperty' not found", exception.Message);
        }

        [Fact]
        public void GetColumnType_ReturnsExactPropertyType_IncludingNullableWrappers()
        {
            // This test verifies the CRITICAL comment in the original code:
            // "CRITICAL: Return the ACTUAL property type (including nullable wrappers)"
            
            // Act & Assert
            Assert.Equal(typeof(bool?), _metadataProvider.GetColumnType("custom_table", "IsDeleted"));
            Assert.Equal(typeof(DateOnly?), _metadataProvider.GetColumnType("another_table", "DueDate"));
            Assert.Equal(typeof(int), _metadataProvider.GetColumnType("custom_table", "Id")); // Not nullable
        }

        #endregion

        #region Mock Entities for Testing - Same as ExcelToDatabaseConverterUnitTests

        private class MockEntity
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public bool IsActive { get; set; }
            public bool? IsDeleted { get; set; } // Nullable property
            
            // Private property that should not be accessible
            private string PrivateProperty { get; set; } = string.Empty;
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

        private class NullableEntity
        {
            public int? NullableInt { get; set; }
            public bool? NullableBool { get; set; }
            public DateTime? NullableDateTime { get; set; }
            public DateOnly? NullableDateOnly { get; set; }
        }

        #endregion
    }
}
