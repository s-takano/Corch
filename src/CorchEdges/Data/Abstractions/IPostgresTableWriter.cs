using System.Data;
using System.Data.Common;

namespace CorchEdges.Data.Abstractions;

/// <summary>
/// Defines a contract for writing a set of tables, contained within a <see cref="DataSet"/>,
/// to a PostgreSQL database using a specified connection and transaction.
/// </summary>
public interface IPostgresTableWriter
{
    /// Writes the data from the specified DataSet into a PostgreSQL database using the provided connection and transaction.
    /// This method utilizes PostgreSQL's COPY functionality for efficient bulk insertion of data into database tables.
    /// <param name="tables">
    /// A DataSet containing one or more DataTable objects to be written to the database.
    /// Each DataTable represents a PostgreSQL table, where the table's schema should match the structure of the database table.
    /// </param>
    /// <param name="connection">
    /// The database connection to the PostgreSQL server.
    /// The connection must already be opened prior to calling this method.
    /// </param>
    /// <param name="transaction">
    /// The database transaction within which the write operation will execute.
    /// This ensures the operation is atomic and can be rolled back if necessary.
    /// </param>
    /// <returns>
    /// A Task representing the asynchronous operation.
    /// </returns>
    Task WriteAsync(DataSet tables, DbConnection connection, DbTransaction transaction);
}