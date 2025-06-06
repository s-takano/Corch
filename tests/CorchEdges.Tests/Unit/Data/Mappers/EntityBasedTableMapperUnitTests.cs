// tests/CorchEdges.Tests/Unit/Data/Mappers/EntityBasedTableMapperUnitTests.cs

using CorchEdges.Data.Mappers;
using Xunit;

namespace CorchEdges.Tests.Unit.Data.Mappers
{
    [Trait("Category", "Unit")]
    [Trait("Component", "EntityBasedTableMapper")]
    public class EntityBasedTableMapperUnitTests
    {
        private readonly EntityBasedTableMapper _tableMapper;
        
        public EntityBasedTableMapperUnitTests()
        {
            var customTableMappings = new Dictionary<string, string>
            {
                { "新規to業務管理", "contract_creation" },
                { "契約一覧to業務管理", "contract_current" },
                { "更新to業務管理", "contract_renewal" },
                { "解約to業務管理", "contract_termination" },
                { "processing_log", "processing_log" },
                { "processed_file", "processed_file" },
                { "CustomSheet", "custom_table" },
                { "AnotherSheet", "another_table" }
            };

            _tableMapper = new EntityBasedTableMapper(customTableMappings);
        }

        [Theory]
        [InlineData("新規to業務管理", "contract_creation")]
        [InlineData("契約一覧to業務管理", "contract_current")]
        [InlineData("更新to業務管理", "contract_renewal")]
        [InlineData("解約to業務管理", "contract_termination")]
        [InlineData("processing_log", "processing_log")]
        [InlineData("processed_file", "processed_file")]
        [InlineData("CustomSheet", "custom_table")]
        [InlineData("AnotherSheet", "another_table")]
        public void MapTableName_WithValidMappings_ReturnsCorrectMapping(string originalTableName, string expectedTableName)
        {
            // Act
            var result = _tableMapper.MapTableName(originalTableName);

            // Assert
            Assert.Equal(expectedTableName, result);
        }

        [Theory]
        [InlineData("UnknownSheet")]
        [InlineData("NonExistentTable")]
        [InlineData("InvalidTableName")]
        [InlineData("")]
        [InlineData("   ")]
        public void MapTableName_WithUnknownTableName_ThrowsArgumentException(string unknownTableName)
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => _tableMapper.MapTableName(unknownTableName));
            Assert.Contains("Invalid table name", exception.Message);
            Assert.Contains(unknownTableName, exception.Message);
        }

        [Fact]
        public void MapTableName_WithNullTableName_ThrowsArgumentException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => _tableMapper.MapTableName(null!));
            Assert.Contains("Invalid table name", exception.Message);
        }

        [Fact]
        public void Constructor_WithNullMappings_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => new EntityBasedTableMapper(null!));
            Assert.Equal("mappings", exception.ParamName);
        }

        [Fact]
        public void Constructor_WithEmptyMappings_DoesNotThrow()
        {
            // Arrange
            var emptyMappings = new Dictionary<string, string>();

            // Act & Assert - Should not throw
            var mapper = new EntityBasedTableMapper(emptyMappings);
            Assert.NotNull(mapper);
        }

        [Fact]
        public void MapTableName_WithEmptyMappings_ThrowsArgumentException()
        {
            // Arrange
            var emptyMappings = new Dictionary<string, string>();
            var mapper = new EntityBasedTableMapper(emptyMappings);

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => mapper.MapTableName("AnyTable"));
            Assert.Contains("Invalid table name", exception.Message);
        }

        [Fact]
        public void MapTableName_WithCaseSensitiveMapping_IsExactMatch()
        {
            // Arrange
            var caseSensitiveMappings = new Dictionary<string, string>
            {
                { "TestTable", "test_table" },
                { "testtable", "different_table" }
            };
            var mapper = new EntityBasedTableMapper(caseSensitiveMappings);

            // Act & Assert
            Assert.Equal("test_table", mapper.MapTableName("TestTable"));
            Assert.Equal("different_table", mapper.MapTableName("testtable"));
            
            // Different case should throw exception
            var exception = Assert.Throws<ArgumentException>(() => mapper.MapTableName("TESTTABLE"));
            Assert.Contains("Invalid table name", exception.Message);
        }

        [Fact]
        public void MapTableName_WithSpecialCharacters_HandlesCorrectly()
        {
            // Arrange
            var specialCharMappings = new Dictionary<string, string>
            {
                { "Table-With-Dashes", "table_with_dashes" },
                { "Table With Spaces", "table_with_spaces" },
                { "Table@Special#Chars", "table_special_chars" }
            };
            var mapper = new EntityBasedTableMapper(specialCharMappings);

            // Act & Assert
            Assert.Equal("table_with_dashes", mapper.MapTableName("Table-With-Dashes"));
            Assert.Equal("table_with_spaces", mapper.MapTableName("Table With Spaces"));
            Assert.Equal("table_special_chars", mapper.MapTableName("Table@Special#Chars"));
        }

        [Fact]
        public void MapTableName_WithUnicodeCharacters_HandlesCorrectly()
        {
            // Arrange
            var unicodeMappings = new Dictionary<string, string>
            {
                { "契約管理", "contract_management" },
                { "物件情報", "property_info" },
                { "顧客データ", "customer_data" }
            };
            var mapper = new EntityBasedTableMapper(unicodeMappings);

            // Act & Assert
            Assert.Equal("contract_management", mapper.MapTableName("契約管理"));
            Assert.Equal("property_info", mapper.MapTableName("物件情報"));
            Assert.Equal("customer_data", mapper.MapTableName("顧客データ"));
        }
    }
}