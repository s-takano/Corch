using CorchEdges.Data.Abstractions;
using System.Globalization;
using System.Text;

namespace CorchEdges.Data.Mappers;

/// <summary>
/// Utility class that maps column names from an original representation
/// to a mapped (transformed) representation based on pre-defined mappings,
/// scoped by the table name it belongs to.
/// </summary>
/// <remarks>
/// This class implements the <c>IColumnNameMapper</c> interface, ensuring
/// functionality to map column names for different sets of data, defined
/// by source-specific table mappings.
/// </remarks>
/// <example>
/// Can be used as part of normalization or transformation pipelines where
/// column names must comply with entity definitions.
/// </example>
public class EntityBasedColumnMapper(Dictionary<string, Dictionary<string, string>>? columnMappings)
    : IColumnNameMapper
{
    /// <summary>
    /// Represents the maximum length allowed for PostgreSQL identifiers,
    /// including table names, column names, and other database object identifiers.
    /// </summary>
    /// <remarks>
    /// PostgreSQL restricts the length of identifiers to a maximum of 63 characters.
    /// Identifiers exceeding this limit will cause errors when interacting with the database.
    /// This constant ensures that any identifier used adheres to this constraint.
    /// </remarks>
    private const int PostgreSqlIdentifierMaxLength = 63;

    // PostgreSQL reserved keywords (case-insensitive)
    /// <summary>
    /// A collection of PostgreSQL reserved keywords represented as a case-insensitive HashSet.
    /// This set is used for validating column names to ensure they do not conflict
    /// with reserved keywords in PostgreSQL, which may result in errors during query execution.
    /// </summary>
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

    /// <summary>
    /// Maps the given column name from the specified table to its corresponding mapped column name
    /// using pre-configured mappings.
    /// </summary>
    /// <param name="tableName">The name of the table to look up column mappings for.</param>
    /// <param name="originalColumnName">The original column name to be mapped.</param>
    /// <returns>The mapped column name corresponding to the given table and original column name.</returns>
    /// <exception cref="ArgumentException">Thrown when the table name is unknown, the column name is unmapped,
    /// or either the table name or column name is invalid.</exception>
    public string MapColumnName(string tableName, string originalColumnName)
    {
        var tableColumnMappings = GetColumnMappings(tableName);

        var validatedColumnName = ValidateColumnName(originalColumnName);

        // Check if a column exists in table mappings
        if (!tableColumnMappings.TryGetValue(validatedColumnName, out var mappedColumnName))
            throw new ArgumentException($"Invalid column name '{originalColumnName}' for table '{tableName}'.");

        return mappedColumnName;
    }

    /// <summary>
    /// Retrieves the column mappings for a specified table name from the predefined mappings.
    /// </summary>
    /// <param name="tableName">The name of the table whose column mappings are to be retrieved.</param>
    /// <returns>A dictionary where keys represent original column names and values represent their mapped names.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown if the table name is null, empty, or not present in the column mappings, or if no column mappings are defined.
    /// </exception>
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

    /// <summary>
    /// A dictionary containing column validation rules as key-value pairs,
    /// where each key is the name of a validation rule, and each value is a delegate
    /// that defines the logic for the corresponding validation.
    /// These validation rules are applied to ensure that column names conform to the
    /// expected criteria before mapping or further processing. For instance, validations
    /// may include checking for empty names, ensuring a specific length, or avoiding reserved keywords.
    /// </summary>
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

    /// <summary>
    /// Validates and processes the given column name to ensure it meets all required conditions.
    /// </summary>
    /// <param name="columnName">The column name to be validated. Can be null or empty.</param>
    /// <returns>The trimmed and validated column name as a string. Throws an exception if the name is invalid.</returns>
    public string ValidateColumnName(string? columnName)
    {
        var trimmedName = columnName?.Trim() ?? string.Empty;

        foreach (var validation in ColumnValidations) validation.Value(trimmedName);

        return trimmedName;
    }

    /// <summary>
    /// Validates the characters in a column name to ensure they conform to the rules
    /// for PostgreSQL quoted identifiers. Rejects null characters and control characters
    /// (other than tab) as they are not allowed in PostgreSQL identifiers.
    /// </summary>
    /// <param name="columnName">The column name to validate. Must not contain invalid characters.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the column name contains control characters (other than tab) or null characters,
    /// which are not permitted in PostgreSQL identifiers.
    /// </exception>
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
}