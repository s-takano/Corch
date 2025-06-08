using CorchEdges.Data.Abstractions;
using System.Globalization;
using System.Text;

namespace CorchEdges.Data.Mappers;

public class EntityBasedColumnMapper(Dictionary<string, Dictionary<string, string>>? columnMappings)
    : IColumnNameMapper
{
    private const int PostgreSqlIdentifierMaxLength = 63;

    // PostgreSQL reserved keywords (case-insensitive)
    private static readonly HashSet<string> PostgreSqlReservedKeywords = new(StringComparer.OrdinalIgnoreCase)
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

    public string MapColumnName(string tableName, string originalColumnName)
    {
        var tableColumnMappings = GetColumnMappings(tableName);

        var validatedColumnName = ValidateColumnName(originalColumnName);

        // Check if a column exists in table mappings
        if (!tableColumnMappings.TryGetValue(validatedColumnName, out var mappedColumnName))
            throw new ArgumentException($"Invalid column name '{originalColumnName}' for table '{tableName}'.");

        return mappedColumnName;
    }

    private Dictionary<string, string> GetColumnMappings(string tableName)
    {
        // Check if we have any mappings at all
        if (columnMappings == null)
            throw new ArgumentException("No column mappings available.");

        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be null or empty.", nameof(tableName));

        // Check if a table exists in mappings
        if (!columnMappings.TryGetValue(tableName, out var tableColumnMappings))
            throw new ArgumentException($"Unknown table: {tableName}");

        return tableColumnMappings;
    }

    private Dictionary<string, Action<string>> ColumnValidations { get; } = new Dictionary<string, Action<string>>
    {
        ["NotEmpty"] = (name) =>
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Column name cannot be null or empty.", nameof(name));
        },
        ["Length"] = (name) =>
        {
            if (name.Length > PostgreSqlIdentifierMaxLength)
                throw new InvalidOperationException(
                    $"Column name '{name}' is too long. PostgreSQL identifiers are limited to {PostgreSqlIdentifierMaxLength} characters. Current length: {name.Length}");
        },
        ["StartsWithLetter"] = (name) =>
        {
            if (char.IsDigit(name[0]))
                throw new InvalidOperationException(
                    $"Invalid column name '{name}': PostgreSQL identifiers cannot start with a digit.");
        },
        ["NotReservedKeyword"] = (name) =>
        {
            if (PostgreSqlReservedKeywords.Contains(name))
                throw new InvalidOperationException(
                    $"Column name '{name.ToLower()}' is a PostgreSQL reserved keyword and cannot be used as an identifier.");
        },
        ["Characters"] = ValidateCharacters
    };

    public string ValidateColumnName(string? columnName)
    {
        var trimmedName = columnName?.Trim() ?? string.Empty;

        foreach (var validation in ColumnValidations) validation.Value(trimmedName);

        return trimmedName;
    }

    private static void ValidateCharacters(string columnName)
    {
        // PostgreSQL quoted identifiers can contain almost any character
        // Only reject truly problematic characters (null, control characters)
    
        foreach (char c in columnName)
        {
            // Reject control characters (except tab which might be valid in some contexts)
            if (char.IsControl(c) && c != '\t')
            {
                throw new InvalidOperationException(
                    $"Invalid character in column name '{columnName}': Control character '\\u{(int)c:X4}' is not allowed in PostgreSQL identifiers.");
            }
        
            // Reject null character specifically
            if (c == '\0')
            {
                throw new InvalidOperationException(
                    $"Invalid character in column name '{columnName}': Null character is not allowed in PostgreSQL identifiers.");
            }
        }
    
        // Note: We're being permissive here because PostgreSQL quoted identifiers
        // support Unicode, parentheses, hyphens, and most special characters
        // The database will ultimately enforce its own rules during table creation
    }

    private static bool IsValidPostgreSqlIdentifierChar(char ch)
    {
        var isLetter = char.IsLetter(ch);
        var isDigit = char.IsDigit(ch);
        var isUnderscore = ch == '_';
        var isDollarSign = ch == '$';

        return (isLetter || isDigit || isUnderscore) && !isDollarSign;
    }
}