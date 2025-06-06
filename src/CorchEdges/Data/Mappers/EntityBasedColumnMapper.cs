using CorchEdges.Data.Abstractions;
using System.Globalization;
using System.Text;

namespace CorchEdges.Data.Mappers;

public class EntityBasedColumnMapper : IColumnNameMapper
{
    private readonly Dictionary<string, Dictionary<string, string>>? _columnMappings;

    // PostgreSQL reserved keywords (case-insensitive)
    private static readonly HashSet<string> PostgreSQLReservedKeywords = new(StringComparer.OrdinalIgnoreCase)
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
    };

    public EntityBasedColumnMapper(Dictionary<string, Dictionary<string, string>>? columnMappings)
    {
        _columnMappings = columnMappings;
    }

    public string MapColumnName(string tableName, string originalColumnName)
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be null or empty.", nameof(tableName));

        if (string.IsNullOrWhiteSpace(originalColumnName))
            throw new ArgumentException("Invalid column name: Column name cannot be null or empty.", nameof(originalColumnName));

        // Check if we have any mappings at all
        if (_columnMappings == null)
            throw new ArgumentException("No column mappings available.");

        // Check if table exists in mappings
        if (!_columnMappings.TryGetValue(tableName, out var tableColumnMappings))
            throw new ArgumentException($"Unknown table: {tableName}");

        // Check if column exists in table mappings
        if (!tableColumnMappings.TryGetValue(originalColumnName, out var mappedColumnName))
            throw new ArgumentException($"Invalid column name '{originalColumnName}' for table '{tableName}'.");

        return mappedColumnName;
    }

    public string ValidateColumnName(string columnName)
    {
        // Check for null or empty
        if (string.IsNullOrWhiteSpace(columnName))
            throw new ArgumentException("Column name cannot be null or empty.", nameof(columnName));

        // Trim whitespace for validation
        var trimmedName = columnName.Trim();
        if (string.IsNullOrEmpty(trimmedName))
            throw new ArgumentException("Column name cannot be null or empty.", nameof(columnName));

        // Check length limit (PostgreSQL limit is 63 characters)
        if (trimmedName.Length > 63)
            throw new InvalidOperationException(
                $"Column name '{trimmedName}' is too long. PostgreSQL identifiers are limited to 63 characters. Current length: {trimmedName.Length}");

        // Check if it starts with a digit
        if (char.IsDigit(trimmedName[0]))
            throw new InvalidOperationException(
                $"Invalid column name '{trimmedName}': PostgreSQL identifiers cannot start with a digit.");

        // Check for PostgreSQL reserved keywords (case-insensitive)
        if (PostgreSQLReservedKeywords.Contains(trimmedName))
            throw new InvalidOperationException(
                $"Column name '{trimmedName.ToLower()}' is a PostgreSQL reserved keyword and cannot be used as an identifier.");

        // Check for invalid characters
        ValidateCharacters(trimmedName);

        return columnName; // Return original (untrimmed) name if valid
    }

    private static void ValidateCharacters(string columnName)
    {
        for (int i = 0; i < columnName.Length; i++)
        {
            var ch = columnName[i];
            
            // Valid characters for PostgreSQL identifiers:
            // - Letters (a-z, A-Z) - includes Unicode letters
            // - Digits (0-9) - but not as first character (already checked)
            // - Underscore (_)
            // - Dollar sign ($) - PostgreSQL extension
            
            if (IsValidPostgreSQLIdentifierChar(ch))
                continue;

            // Invalid character found - provide detailed error message
            throw new InvalidOperationException(
                $"Contains Invalid character in original column name '{columnName}': " +
                $"Character '{ch}' at position {i} is not allowed in PostgreSQL identifiers. " +
                $"Valid characters are letters, digits, underscores (_), and dollar signs ($).");
        }
    }

    private static bool IsValidPostgreSQLIdentifierChar(char ch)
    {
        // Allow letters (including Unicode letters)
        if (char.IsLetter(ch))
            return true;

        // Allow digits
        if (char.IsDigit(ch))
            return true;

        // Allow underscore
        if (ch == '_')
            return true;

        // Not Allow dollar sign (PostgreSQL extension)
        if (ch == '$')
            return false;

        return false;
    }

}