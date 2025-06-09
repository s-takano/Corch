// tests/CorchEdges.Tests/Unit/Data/Mappers/EntityBasedColumnMapperUnitTests.cs

using CorchEdges.Data.Mappers;
using Xunit;

namespace CorchEdges.Tests.Unit.Data.Mappers;

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
                    { "礼金(家)", "key_money_home" }, // Parentheses + Japanese (from your real DDL)
                    { "ｱﾊﾟ-ﾄ保険代", "apartment_insurance" }, // Hyphens + Japanese (from your real DDL)
                    { "column with spaces", "column_with_spaces" }, // Spaces
                    { "user@domain", "user_at_domain" }, // At symbol
                    { "temp#table", "temp_hash_table" }, // Hash symbol
                    { "进捗管理ステータス", "progress_status" } // Unicode characters
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


    [Theory]
    [MemberData(nameof(PostgreSqlReservedKeywords))]
    public void MapColumnName_WithPostgreSQLReservedKeywords_ThrowsInvalidOperationException(string reservedKeyword)
    {
        // Arrange
        var columnMappings = new Dictionary<string, Dictionary<string, string>>
        {
            { "TestTable", new Dictionary<string, string> { { reservedKeyword, "mapped_column" } } }
        };
        var mapper = new EntityBasedColumnMapper(columnMappings);

        // Act & Assert - Should fail during validation phase
        var exception = Assert.Throws<InvalidOperationException>(() =>
            mapper.MapColumnName("TestTable", reservedKeyword));

        Assert.Contains("is a PostgreSQL reserved keyword", exception.Message);
        Assert.Contains(reservedKeyword, exception.Message);
    }

    public static IEnumerable<object[]> PostgreSqlReservedKeywords =>
        new string[]
            {
                "select", "from", "where", "insert", "update", "delete", "create", "drop", "alter",
                "table", "column", "index", "primary", "foreign", "key", "constraint", "null", "not",
                "unique", "default", "check", "references", "on", "cascade", "restrict", "set", "user",
                "order", "group", "having", "union", "join", "inner", "left", "right", "full", "outer",
                "cross", "natural", "using", "as", "distinct", "all", "any", "some", "exists", "in",
                "between", "like", "ilike", "similar", "is", "and", "or", "case", "when", "then",
                "else", "end", "grant", "revoke", "commit", "rollback", "transaction", "begin",
                "declare", "if", "while", "for", "loop", "return", "function", "procedure", "trigger",
                "view", "database", "schema", "sequence", "domain", "type", "cast", "analyze", "vacuum",
                "explain", "copy", "truncate", "lock", "unlock", "with", "recursive", "lateral",
                "offset", "limit", "fetch", "first", "last", "only", "rows", "row", "value", "values",
                "interval", "timestamp", "date", "time", "boolean", "integer", "bigint", "smallint",
                "decimal", "numeric", "real", "double", "precision", "varchar", "char", "text", "bytea"
            }
            .Select(keyword => new object[] { keyword });



    [Theory]
    [InlineData("礼金(家)")] // Parentheses + Japanese (from your PostgreSQL DDL)
    [InlineData("ｱﾊﾟ-ﾄ保険代")] // Hyphens + Japanese (from your PostgreSQL DDL)  
    [InlineData("column with spaces")] // Spaces (valid in quoted identifiers)
    [InlineData("user@domain.com")] // At symbol and dots
    [InlineData("temp#123")] // Hash with numbers
    [InlineData("進捗管理ステータス")] // Full-width Japanese characters
    [InlineData("contract_id")] // Standard identifier (still valid)
    [InlineData("Property_No")] // Standard with underscore
    public void MapColumnName_WithValidPostgreSqlCharacters_ReturnsCorrectMapping(string columnName)
    {
        // Arrange - Create mapping that includes the test column
        var columnMappings = new Dictionary<string, Dictionary<string, string>>
        {
            {
                "TestTable", new Dictionary<string, string>
                {
                    { columnName, $"mapped_{columnName.Replace(" ", "_").Replace("#", "hash").Replace("@", "at")}" }
                }
            }
        };
        var mapper = new EntityBasedColumnMapper(columnMappings);

        // Act - This will internally call ValidateColumnName + do the mapping
        var result = mapper.MapColumnName("TestTable", columnName);

        // Assert - Should successfully validate and return mapped name
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        // The fact that it didn't throw means validation passed
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MapColumnName_WithEmptyOrNullColumnName_ThrowsArgumentException(string? columnName)
    {
        // Arrange
        var columnMappings = new Dictionary<string, Dictionary<string, string>>
        {
            { "TestTable", new Dictionary<string, string>() }
        };
        var mapper = new EntityBasedColumnMapper(columnMappings);

        // Act & Assert - Should fail during validation phase
        var exception = Assert.Throws<ArgumentException>(() =>
            mapper.MapColumnName("TestTable", columnName!));

        Assert.Contains("Column name cannot be null or empty", exception.Message);
    }

    [Theory]
    [InlineData("1invalid")]
    [InlineData("2column")]
    [InlineData("9test")]
    public void MapColumnName_WithNameStartingWithDigit_ThrowsInvalidOperationException(string columnName)
    {
        // Arrange
        var columnMappings = new Dictionary<string, Dictionary<string, string>>
        {
            { "TestTable", new Dictionary<string, string> { { columnName, "mapped_column" } } }
        };
        var mapper = new EntityBasedColumnMapper(columnMappings);

        // Act & Assert - Should fail during validation phase
        var exception = Assert.Throws<InvalidOperationException>(() =>
            mapper.MapColumnName("TestTable", columnName));

        Assert.Contains("PostgreSQL identifiers cannot start with a digit", exception.Message);
    }

    [Theory]
    [InlineData("column\x00name")] // Null character - truly invalid
    [InlineData("column\x01name")] // Control character - invalid  
    [InlineData("column\x02name")] // Another control character - invalid
    [InlineData("column\rname")] // Carriage return - control character
    [InlineData("column\nname")] // Newline - control character
    public void MapColumnName_WithProblematicCharacters_ThrowsInvalidOperationException(string columnName)
    {
        // Arrange
        var columnMappings = new Dictionary<string, Dictionary<string, string>>
        {
            { "TestTable", new Dictionary<string, string> { { columnName, "mapped_column" } } }
        };
        var mapper = new EntityBasedColumnMapper(columnMappings);

        // Act & Assert - Should fail during validation phase
        var exception = Assert.Throws<InvalidOperationException>(() =>
            mapper.MapColumnName("TestTable", columnName));

        Assert.Contains("Invalid character in column name", exception.Message);
        Assert.Contains("Control character", exception.Message);
    }

    [Fact]
    public void MapColumnName_WithTooLongName_ThrowsInvalidOperationException()
    {
        // Arrange - Create a column name longer than 63 characters
        var longColumnName = new string('a', 64); // 64 characters - too long for PostgreSQL
        var columnMappings = new Dictionary<string, Dictionary<string, string>>
        {
            { "TestTable", new Dictionary<string, string> { { longColumnName, "mapped_column" } } }
        };
        var mapper = new EntityBasedColumnMapper(columnMappings);

        // Act & Assert - Should fail during validation phase
        var exception = Assert.Throws<InvalidOperationException>(() =>
            mapper.MapColumnName("TestTable", longColumnName));

        Assert.Contains("too long", exception.Message);
        Assert.Contains("63 characters", exception.Message);
    }

    [Fact]
    public void MapColumnName_WithExactly63Characters_ReturnsCorrectMapping()
    {
        // Arrange - Create a column name exactly 63 characters (PostgreSQL limit)
        var exactLengthColumnName = new string('a', 63);
        var columnMappings = new Dictionary<string, Dictionary<string, string>>
        {
            { "TestTable", new Dictionary<string, string> { { exactLengthColumnName, "mapped_column" } } }
        };
        var mapper = new EntityBasedColumnMapper(columnMappings);

        // Act - Should pass validation and return mapped name
        var result = mapper.MapColumnName("TestTable", exactLengthColumnName);

        // Assert
        Assert.Equal("mapped_column", result);
    }


    [Theory]
    [InlineData("SELECT")] // Uppercase
    [InlineData("From")] // Mixed case
    [InlineData("TIMESTAMP")] // Uppercase timestamp
    public void MapColumnName_WithReservedKeywordsDifferentCase_ThrowsInvalidOperationException(
        string reservedKeyword)
    {
        // Arrange
        var columnMappings = new Dictionary<string, Dictionary<string, string>>
        {
            { "TestTable", new Dictionary<string, string> { { reservedKeyword, "mapped_column" } } }
        };
        var mapper = new EntityBasedColumnMapper(columnMappings);

        // Act & Assert - Should fail (case-insensitive validation)
        var exception = Assert.Throws<InvalidOperationException>(() =>
            mapper.MapColumnName("TestTable", reservedKeyword));

        Assert.Contains("PostgreSQL reserved keyword", exception.Message);
    }

    [Theory]
    [InlineData("user_id")] // Contains "user" but not exactly "user"
    [InlineData("table_name")] // Contains "table" but not exactly "table"
    [InlineData("timestamp_col")] // Contains "timestamp" but not exactly "timestamp"
    [InlineData("select_option")] // Contains "select" but not exactly "select"
    public void MapColumnName_WithKeywordsAsPartOfName_ReturnsCorrectMapping(string columnName)
    {
        // Arrange
        var columnMappings = new Dictionary<string, Dictionary<string, string>>
        {
            { "TestTable", new Dictionary<string, string> { { columnName, "mapped_column" } } }
        };
        var mapper = new EntityBasedColumnMapper(columnMappings);

        // Act - Should pass validation since these are not exact keyword matches
        var result = mapper.MapColumnName("TestTable", columnName);

        // Assert
        Assert.Equal("mapped_column", result);
    }

    [Fact]
    public void MapColumnName_WithTabCharacter_IsAllowed()
    {
        // Arrange - Tab is specifically allowed in the relaxed validation
        var columnWithTab = "column\tname";
        var columnMappings = new Dictionary<string, Dictionary<string, string>>
        {
            { "TestTable", new Dictionary<string, string> { { columnWithTab, "mapped_column" } } }
        };
        var mapper = new EntityBasedColumnMapper(columnMappings);

        // Act - Should pass validation
        var result = mapper.MapColumnName("TestTable", columnWithTab);

        // Assert
        Assert.Equal("mapped_column", result);
    }
}