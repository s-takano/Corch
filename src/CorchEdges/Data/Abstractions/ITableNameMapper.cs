namespace CorchEdges.Data.Abstractions;

/// <summary>
/// Defines a contract for mapping table names from their original names to mapped names
/// based on specific rules or mappings.
/// </summary>
public interface ITableNameMapper
{
    /// <summary>
    /// Maps the original table name to a new table name based on predefined mappings.
    /// </summary>
    /// <param name="originalTableName">The name of the table to be mapped.</param>
    /// <returns>The mapped table name if a mapping exists; otherwise, throws an exception.</returns>
    string MapTableName(string originalTableName);
}