// tests/CorchEdges.Tests/Unit/Data/Mappers/EntityBasedColumnMapperUnitTests.cs

using CorchEdges.Data.Mappers;
using Xunit;

namespace CorchEdges.Tests.Unit.Data.Mappers
{
    [Trait("Category", "Unit")]
    [Trait("Component", "EntityBasedColumnMapper")]
    public class EntityBasedColumnMapperUnitTests
    {
        private readonly EntityBasedColumnMapper _columnMapper;

        public EntityBasedColumnMapperUnitTests()
        {
            var customColumnMappings = new Dictionary<string, Dictionary<string, string>>
            {
                {
                    "CustomSheet", new Dictionary<string, string> // ✅ This should be source name
                    {
                        { "OriginalId", "Id" },
                        { "OriginalName", "Name" }
                    }
                },
                {
                    "AnotherSheet", new Dictionary<string, string> // ✅ This should be source name  
                    {
                        { "契約ID", "Id" },
                        { "物件名", "Title" }
                    }
                },
                {
                    "TestSheet", new Dictionary<string, string> // ✅ This should be source name
                    {
                        { "Code", "ProductCode" },
                        { "Description", "ProductDescription" }
                    }
                }
            };

            _columnMapper = new EntityBasedColumnMapper(customColumnMappings);
        }

        [Theory]
        [InlineData("CustomSheet", "OriginalId", "Id")] // Changed from "custom_table"
        [InlineData("CustomSheet", "OriginalName", "Name")] // Changed from "custom_table"
        [InlineData("AnotherSheet", "契約ID", "Id")] // Changed from "another_table"
        [InlineData("AnotherSheet", "物件名", "Title")] // Changed from "another_table"
        [InlineData("TestSheet", "Code", "ProductCode")] // Changed from "test_data_table"
        [InlineData("TestSheet", "Description", "ProductDescription")] // Changed from "test_data_table"
        public void MapColumnName_WithCustomMappings_ReturnsCorrectMapping(string tableName, string originalColumn,
            string expectedColumn)
        {
            // Act
            var result = _columnMapper.MapColumnName(tableName, originalColumn);

            // Assert
            Assert.Equal(expectedColumn, result);
        }

        [Theory]
        [InlineData("CustomSheet", "UnmappedColumn")]
        [InlineData("AnotherSheet", "SomeOtherColumn")]
        [InlineData("TestSheet", "RandomColumn")]
        public void MapColumnName_WithUnmappedColumn_ThrowsArgumentException(string tableName, string originalColumn)
        {
            // Act & Assert
            var exception =
                Assert.Throws<ArgumentException>(() => _columnMapper.MapColumnName(tableName, originalColumn));
            Assert.Contains("Invalid column", exception.Message);
            Assert.Contains(originalColumn, exception.Message);
            Assert.Contains(tableName, exception.Message);
        }

        [Theory]
        [InlineData("unknown_table", "AnyColumn")]
        public void MapColumnName_WithUnknownTable_ThrowsArgumentException(string tableName, string originalColumn)
        {
            // Act & Assert
            var exception =
                Assert.Throws<ArgumentException>(() => _columnMapper.MapColumnName(tableName, originalColumn));
            Assert.Contains("Unknown table", exception.Message);
            Assert.Contains(tableName, exception.Message);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void MapColumnName_WithEmptyOrNullColumnName_ThrowsArgumentException(string? columnName)
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                _columnMapper.MapColumnName("CustomSheet", columnName!));
            Assert.Contains("Column name cannot be null or empty", exception.Message);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void MapColumnName_WithEmptyOrNullTableName_ThrowsArgumentException(string? tableName)
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                _columnMapper.MapColumnName(tableName!, "AnyColumn"));
            Assert.Contains("Table name cannot be null or empty", exception.Message);
        }

        [Fact]
        public void Constructor_WithNullMappings_DoesNotThrow()
        {
            // Act & Assert - Constructor should not throw
            var mapper = new EntityBasedColumnMapper(null!);
            Assert.NotNull(mapper);
        }

        [Fact]
        public void Constructor_WithEmptyMappings_DoesNotThrow()
        {
            // Arrange
            var emptyMappings = new Dictionary<string, Dictionary<string, string>>();

            // Act & Assert - Constructor should not throw
            var mapper = new EntityBasedColumnMapper(emptyMappings);
            Assert.NotNull(mapper);
        }

        [Fact]
        public void MapColumnName_WithEmptyTableMappings_ThrowsArgumentException()
        {
            // Arrange
            var emptyMappings = new Dictionary<string, Dictionary<string, string>>();
            var mapper = new EntityBasedColumnMapper(emptyMappings);

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                mapper.MapColumnName("AnyTable", "AnyColumn"));
            Assert.Contains("Unknown table", exception.Message);
        }

        [Fact]
        public void MapColumnName_WithEmptyColumnMappingsForTable_ThrowsArgumentException()
        {
            // Arrange
            var mappingsWithEmptyTable = new Dictionary<string, Dictionary<string, string>>
            {
                { "empty_table", new Dictionary<string, string>() }
            };
            var mapper = new EntityBasedColumnMapper(mappingsWithEmptyTable);

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                mapper.MapColumnName("empty_table", "AnyColumn"));
            Assert.Contains("Invalid column name", exception.Message);
            Assert.Contains("AnyColumn", exception.Message);
        }

        [Fact]
        public void MapColumnName_WithCaseSensitiveMapping_IsExactMatch()
        {
            // Arrange
            var caseSensitiveMappings = new Dictionary<string, Dictionary<string, string>>
            {
                {
                    "case_table", new Dictionary<string, string>
                    {
                        { "TestColumn", "test_column" },
                        { "testcolumn", "different_column" }
                    }
                }
            };
            var mapper = new EntityBasedColumnMapper(caseSensitiveMappings);

            // Act & Assert
            Assert.Equal("test_column", mapper.MapColumnName("case_table", "TestColumn"));
            Assert.Equal("different_column", mapper.MapColumnName("case_table", "testcolumn"));

            // Different case should throw exception
            var exception = Assert.Throws<ArgumentException>(() =>
                mapper.MapColumnName("case_table", "TESTCOLUMN"));
            Assert.Contains("Invalid column name", exception.Message);
        }

        [Fact]
        public void MapColumnName_WithValidPostgreSqlSpecialCharacters_HandlesCorrectly()
        {
            // Arrange - Test column mappings that include PostgreSQL-valid special characters
            var columnMappings = new Dictionary<string, Dictionary<string, string>>
            {
                {
                    "TestTable", new Dictionary<string, string>
                    {
                        { "礼金(家)", "key_money_home" },           // Parentheses + Japanese (from your real DDL)
                        { "ｱﾊﾟ-ﾄ保険代", "apartment_insurance" },    // Hyphens + Japanese (from your real DDL)
                        { "column with spaces", "column_with_spaces" }, // Spaces
                        { "user@domain", "user_at_domain" },           // At symbol
                        { "temp#table", "temp_hash_table" },           // Hash symbol
                        { "进捗管理ステータス", "progress_status" }        // Unicode characters
                    }
                }
            };

            var mapper = new EntityBasedColumnMapper(columnMappings);

            // Act & Assert - All these should pass validation and return mapped names
            Assert.Equal("key_money_home", mapper.MapColumnName("TestTable", "礼金(家)"));
            Assert.Equal("apartment_insurance", mapper.MapColumnName("TestTable", "ｱﾊﾟ-ﾄ保険代"));
            Assert.Equal("column_with_spaces", mapper.MapColumnName("TestTable", "column with spaces"));
            Assert.Equal("user_at_domain", mapper.MapColumnName("TestTable", "user@domain"));
            Assert.Equal("temp_hash_table", mapper.MapColumnName("TestTable", "temp#table"));
            Assert.Equal("progress_status", mapper.MapColumnName("TestTable", "进捗管理ステータス"));
        }

        [Fact]
        public void MapColumnName_WithUnicodeCharacters_HandlesCorrectly()
        {
            // Arrange
            var unicodeMappings = new Dictionary<string, Dictionary<string, string>>
            {
                {
                    "unicode_table", new Dictionary<string, string>
                    {
                        { "契約管理", "contract_management" },
                        { "物件情報", "property_info" },
                        { "顧客データ", "customer_data" }
                    }
                }
            };
            var mapper = new EntityBasedColumnMapper(unicodeMappings);

            // Act & Assert
            Assert.Equal("contract_management", mapper.MapColumnName("unicode_table", "契約管理"));
            Assert.Equal("property_info", mapper.MapColumnName("unicode_table", "物件情報"));
            Assert.Equal("customer_data", mapper.MapColumnName("unicode_table", "顧客データ"));
        }

        [Fact]
        public void MapColumnName_WithUnknownUnicodeColumn_ThrowsArgumentException()
        {
            // Arrange
            var unicodeMappings = new Dictionary<string, Dictionary<string, string>>
            {
                {
                    "unicode_table", new Dictionary<string, string>
                    {
                        { "契約管理", "contract_management" }
                    }
                }
            };
            var mapper = new EntityBasedColumnMapper(unicodeMappings);

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                mapper.MapColumnName("unicode_table", "未知の列"));
            Assert.Contains("Invalid column", exception.Message);
            Assert.Contains("未知の列", exception.Message);
        }

        // Add these tests to the existing EntityBasedColumnMapperUnitTests class

        #region ValidateColumnName Tests


        [Theory]
        [InlineData("礼金(家)")]        // Parentheses + Japanese (from your PostgreSQL DDL)
        [InlineData("ｱﾊﾟ-ﾄ保険代")]     // Hyphens + Japanese (from your PostgreSQL DDL)  
        [InlineData("column with spaces")]  // Spaces (valid in quoted identifiers)
        [InlineData("user@domain.com")]     // At symbol and dots
        [InlineData("temp#123")]            // Hash with numbers
        [InlineData("進捗管理ステータス")]     // Full-width Japanese characters
        [InlineData("contract_id")]         // Standard identifier (still valid)
        [InlineData("Property_No")]         // Standard with underscore
        public void ValidateColumnName_WithValidPostgreSqlCharacters_ReturnsOriginalName(string columnName)
        {
            // Arrange
            var mapper = new EntityBasedColumnMapper(new Dictionary<string, Dictionary<string, string>>());

            // Act
            var result = mapper.ValidateColumnName(columnName);

            // Assert - Should pass validation and return trimmed original name
            Assert.Equal(columnName.Trim(), result);
        }


        [Theory]
        [InlineData("column\x00name")]     // Null character - truly invalid
        [InlineData("column\x01name")]     // Control character - invalid  
        [InlineData("column\x02name")]     // Another control character - invalid
        [InlineData("column\rname")]       // Carriage return - control character
        [InlineData("column\nname")]       // Newline - control character
        public void ValidateColumnName_WithProblematicCharacters_ThrowsInvalidOperationException(string columnName)
        {
            // Arrange
            var mapper = new EntityBasedColumnMapper(new Dictionary<string, Dictionary<string, string>>());

            // Act & Assert - Only truly problematic characters should be rejected
            var exception = Assert.Throws<InvalidOperationException>(() => 
                mapper.ValidateColumnName(columnName));
    
            Assert.Contains("Invalid character in column name", exception.Message);
            Assert.Contains("Control character", exception.Message);
        }

        
        [Fact]
        public void ValidateColumnName_WithTabCharacter_IsAllowed()
        {
            // Arrange - Tab is specifically allowed in the relaxed validation
            var mapper = new EntityBasedColumnMapper(new Dictionary<string, Dictionary<string, string>>());
            var columnWithTab = "column\tname";

            // Act
            var result = mapper.ValidateColumnName(columnWithTab);

            // Assert - Tab should be allowed
            Assert.Equal(columnWithTab.Trim(), result);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void ValidateColumnName_WithEmptyOrNullName_ThrowsArgumentException(string? columnName)
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => _columnMapper.ValidateColumnName(columnName!));
            Assert.Contains("Column name cannot be null or empty", exception.Message);
        }

        [Theory]
        [InlineData("1InvalidColumn")]
        [InlineData("2StartingWithDigit")]
        [InlineData("9column")]
        [InlineData("0test")]
        public void ValidateColumnName_WithNameStartingWithDigit_ThrowsInvalidOperationException(string columnName)
        {
            // Act & Assert
            var exception =
                Assert.Throws<InvalidOperationException>(() => _columnMapper.ValidateColumnName(columnName));
            Assert.Contains("cannot start with a digit", exception.Message);
            Assert.Contains(columnName, exception.Message);
        }
        
        [Fact]
        public void ValidateColumnName_WithTooLongName_ThrowsInvalidOperationException()
        {
            // Arrange - Create a string longer than 63 characters (PostgreSQL limit)
            var longColumnName = new string('a', 64);

            // Act & Assert
            var exception =
                Assert.Throws<InvalidOperationException>(() => _columnMapper.ValidateColumnName(longColumnName));
            Assert.Contains("is too long", exception.Message);
            Assert.Contains("PostgreSQL identifiers are limited to 63 characters", exception.Message);
            Assert.Contains(longColumnName, exception.Message);
        }

        [Fact]
        public void ValidateColumnName_WithExactly63Characters_ReturnsOriginalName()
        {
            // Arrange - Create a string exactly 63 characters (PostgreSQL limit)
            var exactLimitColumnName = new string('a', 63);

            // Act
            var result = _columnMapper.ValidateColumnName(exactLimitColumnName);

            // Assert
            Assert.Equal(exactLimitColumnName, result);
        }

        [Theory]
        [InlineData("select")] // PostgreSQL reserved keyword
        [InlineData("from")]
        [InlineData("where")]
        [InlineData("insert")]
        [InlineData("update")]
        [InlineData("delete")]
        [InlineData("create")]
        [InlineData("drop")]
        [InlineData("alter")]
        [InlineData("table")]
        [InlineData("column")]
        [InlineData("index")]
        [InlineData("primary")]
        [InlineData("foreign")]
        [InlineData("key")]
        [InlineData("constraint")]
        [InlineData("null")]
        [InlineData("not")]
        [InlineData("unique")]
        [InlineData("default")]
        [InlineData("check")]
        [InlineData("references")]
        [InlineData("on")]
        [InlineData("cascade")]
        [InlineData("restrict")]
        [InlineData("set")]
        [InlineData("user")]
        [InlineData("order")]
        [InlineData("group")]
        [InlineData("having")]
        [InlineData("union")]
        [InlineData("join")]
        [InlineData("inner")]
        [InlineData("left")]
        [InlineData("right")]
        [InlineData("full")]
        [InlineData("outer")]
        [InlineData("cross")]
        [InlineData("natural")]
        [InlineData("using")]
        [InlineData("as")]
        [InlineData("distinct")]
        [InlineData("all")]
        [InlineData("any")]
        [InlineData("some")]
        [InlineData("exists")]
        [InlineData("in")]
        [InlineData("between")]
        [InlineData("like")]
        [InlineData("ilike")]
        [InlineData("similar")]
        [InlineData("is")]
        [InlineData("and")]
        [InlineData("or")]
        [InlineData("case")]
        [InlineData("when")]
        [InlineData("then")]
        [InlineData("else")]
        [InlineData("end")]
        public void ValidateColumnName_WithPostgreSQLReservedKeywords_ThrowsInvalidOperationException(
            string reservedKeyword)
        {
            // Act & Assert
            var exception =
                Assert.Throws<InvalidOperationException>(() => _columnMapper.ValidateColumnName(reservedKeyword));
            Assert.Contains("is a PostgreSQL reserved keyword", exception.Message);
            Assert.Contains(reservedKeyword, exception.Message);
        }

        [Theory]
        [InlineData("SELECT")] // Case variations of reserved keywords
        [InlineData("From")]
        [InlineData("WHERE")]
        [InlineData("Insert")]
        [InlineData("UPDATE")]
        [InlineData("Delete")]
        public void ValidateColumnName_WithPostgreSQLReservedKeywordsDifferentCase_ThrowsInvalidOperationException(
            string reservedKeyword)
        {
            // Act & Assert
            var exception =
                Assert.Throws<InvalidOperationException>(() => _columnMapper.ValidateColumnName(reservedKeyword));
            Assert.Contains("is a PostgreSQL reserved keyword", exception.Message);
            Assert.Contains(reservedKeyword.ToLower(), exception.Message);
        }

        [Theory]
        [InlineData("select_column")] // Keywords as part of column name should be OK
        [InlineData("user_id")]
        [InlineData("table_name")]
        [InlineData("from_date")]
        [InlineData("where_clause")]
        [InlineData("my_select")]
        [InlineData("data_from")]
        public void ValidateColumnName_WithKeywordsAsPartOfName_ReturnsOriginalName(string columnName)
        {
            // Act
            var result = _columnMapper.ValidateColumnName(columnName);

            // Assert
            Assert.Equal(columnName, result);
        }


        [Theory]
        [InlineData("_column_with_underscore")]
        [InlineData("column_with_underscore_")]
        [InlineData("column_with_multiple_underscores")]
        [InlineData("_")]
        [InlineData("__double_underscore")]
        public void ValidateColumnName_WithValidUnderscores_ReturnsOriginalName(string columnName)
        {
            // Act
            var result = _columnMapper.ValidateColumnName(columnName);

            // Assert
            Assert.Equal(columnName, result);
        }

        [Theory]
        [InlineData("測定値")] // Japanese
        [InlineData("數量")] // Chinese
        [InlineData("количество")] // Russian (Cyrillic)
        [InlineData("ποσότητα")] // Greek
        [InlineData("مقدار")] // Arabic
        [InlineData("סכום")] // Hebrew
        public void ValidateColumnName_WithValidUnicodeCharacters_ReturnsOriginalName(string columnName)
        {
            // Act
            var result = _columnMapper.ValidateColumnName(columnName);

            // Assert
            Assert.Equal(columnName, result);
        }

        #endregion
    }
}