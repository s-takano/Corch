namespace CorchEdges.Data.Abstractions;

/// <summary>
/// Defines methods for retrieving metadata about database entities, such as tables and columns.
/// </summary>
public interface IEntityMetadataProvider
{
    /// <summary>
    /// Retrieves the data type of a specified column in a specified table.
    /// </summary>
    /// <param name="tableName">The name of the table containing the column.</param>
    /// <param name="columnName">The name of the column whose type is being retrieved.</param>
    /// <returns>The <see cref="Type"/> representing the column's data type.</returns>
    Type GetColumnType(string tableName, string columnName);

    /// <summary>
    /// Determines whether the specified table exists within the metadata provider.
    /// </summary>
    /// <param name="tableName">The name of the table to check for existence.</param>
    /// <returns>true if the table exists; otherwise, false.</returns>
    bool HasTable(string tableName);

    /// Determines whether a specified column exists within a specified table.
    /// <param name="tableName">
    /// The name of the table to check for the existence of the column.
    /// </param>
    /// <param name="columnName">
    /// The name of the column to check for in the specified table.
    /// </param>
    /// <returns>
    /// True if the specified column exists in the table; otherwise, false.
    /// </returns>
    bool HasColumn(string tableName, string columnName);
}