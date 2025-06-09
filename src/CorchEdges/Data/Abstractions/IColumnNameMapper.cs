
namespace CorchEdges.Data.Abstractions;

/// <summary>
/// Defines a contract for mapping column names from their original format
/// to a desired format, typically for use in database or entity mapping scenarios.
/// </summary>
public interface IColumnNameMapper
{
    /// <summary>
    /// Maps the original column name from a given table to a standardized or transformed column name
    /// using predefined mappings.
    /// </summary>
    /// <param name="originalTableName">The name of the table containing the column.</param>
    /// <param name="originalColumnName">The original column name to be mapped.</param>
    /// <returns>A string representing the mapped column name.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown if the provided column name is invalid or not found within the specified table mappings.
    /// </exception>
    string MapColumnName(string originalTableName, string originalColumnName);
}