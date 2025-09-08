using System.Data;
using System.Data.Common;

namespace CorchEdges.Data.Abstractions;

/// <summary>
/// Defines a contract for writing data to a database.
/// </summary>
public interface IDatabaseWriter
{
    /// <summary>
    /// Writes the data contained in the provided DataSet to the specified database context using the given connection and transaction.
    /// </summary>
    /// <param name="tables">A DataSet object containing the data to be written to the database.</param>
    /// <param name="context">The database context used to perform the write operation.</param>
    /// <param name="connection">The database connection to be used during the write operation.</param>
    /// <param name="transaction">The database transaction under which the write operation will be executed.</param>
    /// <returns>A Task representing the asynchronous operation of writing data to the database.</returns>
    Task<int> WriteAsync(DataSet tables, EdgesDbContext context, DbConnection connection, DbTransaction transaction);
}