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
                    "CustomSheet", new Dictionary<string, string>  // ✅ This should be source name
                    {
                        { "OriginalId", "Id" },
                        { "OriginalName", "Name" }
                    }
                },
                {
                    "AnotherSheet", new Dictionary<string, string>  // ✅ This should be source name  
                    {
                        { "契約ID", "Id" },
                        { "物件名", "Title" }
                    }
                },
                {
                    "TestSheet", new Dictionary<string, string>  // ✅ This should be source name
                    {
                        { "Code", "ProductCode" },
                        { "Description", "ProductDescription" }
                    }
                }
            };

            _columnMapper = new EntityBasedColumnMapper(customColumnMappings);
        }
        
        [Theory]
        [InlineData("CustomSheet", "OriginalId", "Id")]           // Changed from "custom_table"
        [InlineData("CustomSheet", "OriginalName", "Name")]       // Changed from "custom_table"
        [InlineData("AnotherSheet", "契約ID", "Id")]               // Changed from "another_table"
        [InlineData("AnotherSheet", "物件名", "Title")]            // Changed from "another_table"
        [InlineData("TestSheet", "Code", "ProductCode")]           // Changed from "test_data_table"
        [InlineData("TestSheet", "Description", "ProductDescription")] // Changed from "test_data_table"
        public void MapColumnName_WithCustomMappings_ReturnsCorrectMapping(string tableName, string originalColumn, string expectedColumn)
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
            var exception = Assert.Throws<ArgumentException>(() => _columnMapper.MapColumnName(tableName, originalColumn));
            Assert.Contains("Invalid column", exception.Message);
            Assert.Contains(originalColumn, exception.Message);
            Assert.Contains(tableName, exception.Message);
        }

        [Theory]
        [InlineData("unknown_table", "AnyColumn")]
        public void MapColumnName_WithUnknownTable_ThrowsArgumentException(string tableName, string originalColumn)
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => _columnMapper.MapColumnName(tableName, originalColumn));
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
                _columnMapper.MapColumnName("custom_table", columnName!));
            Assert.Contains("Invalid column name", exception.Message);
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
        public void MapColumnName_WithSpecialCharacters_HandlesCorrectly()
        {
            // Arrange
            var specialCharMappings = new Dictionary<string, Dictionary<string, string>>
            {
                {
                    "special_table", new Dictionary<string, string>
                    {
                        { "Column-With-Dashes", "column_with_dashes" },
                        { "Column With Spaces", "column_with_spaces" },
                        { "Column@Special#Chars", "column_special_chars" }
                    }
                }
            };
            var mapper = new EntityBasedColumnMapper(specialCharMappings);

            // Act & Assert
            Assert.Equal("column_with_dashes", mapper.MapColumnName("special_table", "Column-With-Dashes"));
            Assert.Equal("column_with_spaces", mapper.MapColumnName("special_table", "Column With Spaces"));
            Assert.Equal("column_special_chars", mapper.MapColumnName("special_table", "Column@Special#Chars"));
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
        [InlineData("ValidColumnName")]
        [InlineData("Valid_Column_Name")]
        [InlineData("ValidColumnName123")]
        [InlineData("_ValidColumnName")]
        [InlineData("契約ID")] // Unicode characters
        [InlineData("物件名")] // Japanese characters
        [InlineData("valid_column_name")]
        [InlineData("VALID_COLUMN_NAME")]
        [InlineData("column1")]
        [InlineData("a")]
        [InlineData("a_very_long_column_name_that_is_still_within_postgresql_limits")]
        public void ValidateColumnName_WithValidPostgreSQLNames_ReturnsOriginalName(string columnName)
        {
            // Act
            var result = _columnMapper.ValidateColumnName(columnName);

            // Assert
            Assert.Equal(columnName, result);
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
            var exception = Assert.Throws<InvalidOperationException>(() => _columnMapper.ValidateColumnName(columnName));
            Assert.Contains("cannot start with a digit", exception.Message);
            Assert.Contains(columnName, exception.Message);
        }

        [Theory]
        [InlineData("Invalid-Column")]
        [InlineData("Invalid Column")]
        [InlineData("Invalid@Column")]
        [InlineData("Invalid#Column")]
        [InlineData("Invalid%Column")]
        [InlineData("Invalid&Column")]
        [InlineData("Invalid*Column")]
        [InlineData("Invalid+Column")]
        [InlineData("Invalid=Column")]
        [InlineData("Invalid?Column")]
        [InlineData("Invalid!Column")]
        [InlineData("Invalid.Column")]
        [InlineData("Invalid,Column")]
        [InlineData("Invalid;Column")]
        [InlineData("Invalid:Column")]
        [InlineData("Invalid'Column")]
        [InlineData("Invalid\"Column")]
        [InlineData("Invalid[Column")]
        [InlineData("Invalid]Column")]
        [InlineData("Invalid{Column")]
        [InlineData("Invalid}Column")]
        [InlineData("Invalid(Column")]
        [InlineData("Invalid)Column")]
        [InlineData("Invalid<Column")]
        [InlineData("Invalid>Column")]
        [InlineData("Invalid|Column")]
        [InlineData("Invalid\\Column")]
        [InlineData("Invalid/Column")]
        [InlineData("Invalid`Column")]
        [InlineData("Invalid~Column")]
        public void ValidateColumnName_WithInvalidCharacters_ThrowsInvalidOperationException(string columnName)
        {
            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => _columnMapper.ValidateColumnName(columnName));
            Assert.Contains("contains invalid character", exception.Message.ToLower());
            Assert.Contains(columnName, exception.Message);
        }

        [Fact]
        public void ValidateColumnName_WithTooLongName_ThrowsInvalidOperationException()
        {
            // Arrange - Create a string longer than 63 characters (PostgreSQL limit)
            var longColumnName = new string('a', 64);

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => _columnMapper.ValidateColumnName(longColumnName));
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
        public void ValidateColumnName_WithPostgreSQLReservedKeywords_ThrowsInvalidOperationException(string reservedKeyword)
        {
            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => _columnMapper.ValidateColumnName(reservedKeyword));
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
        public void ValidateColumnName_WithPostgreSQLReservedKeywordsDifferentCase_ThrowsInvalidOperationException(string reservedKeyword)
        {
            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => _columnMapper.ValidateColumnName(reservedKeyword));
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
        [InlineData("Contract-ID", "Invalid character detected in original column name 'Contract-ID': Character '-' at position 8 is not allowed in PostgreSQL identifiers")]
        [InlineData("Property Name", "Invalid character detected in original column name 'Property Name': Character ' ' at position 8 is not allowed in PostgreSQL identifiers")]
        [InlineData("Customer@Email", "Invalid character detected in original column name 'Customer@Email': Character '@' at position 8 is not allowed in PostgreSQL identifiers")]
        [InlineData("Price$Amount", "Invalid character detected in original column name 'Price$Amount': Character '$' at position 5 is not allowed in PostgreSQL identifiers")]
        [InlineData("1StartingWithDigit", "Invalid column name '1StartingWithDigit': PostgreSQL identifiers cannot start with a digit")]
        public void ValidateColumnName_WithSpecificInvalidCharacters_ContainsDetailedErrorMessage(string columnName, string expectedErrorPattern)
        {
            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => _columnMapper.ValidateColumnName(columnName));
            
            // Check that the error message contains helpful information about the specific issue
            Assert.Contains(columnName, exception.Message);
            
            if (expectedErrorPattern.Contains("Character"))
            {
                Assert.Contains("contains invalid character", exception.Message.ToLower());
                Assert.Contains("is not allowed in PostgreSQL identifiers", exception.Message);
            }
            else if (expectedErrorPattern.Contains("start with a digit"))
            {
                Assert.Contains("cannot start with a digit", exception.Message);
            }
        }

        [Fact]
        public void ValidateColumnName_WithMultipleInvalidCharacters_ReportsFirstInvalidCharacter()
        {
            // Arrange
            var columnName = "Invalid-Name@Test"; // Multiple invalid characters

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => _columnMapper.ValidateColumnName(columnName));
            
            // Should report the first invalid character encountered
            Assert.Contains("contains invalid character", exception.Message.ToLower());
            Assert.Contains("Invalid-Name@Test", exception.Message);
            Assert.Contains("Character '-'", exception.Message); // First invalid character
            Assert.Contains("position 7", exception.Message);
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