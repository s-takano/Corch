using System.Data;
using System.Data.Common;
using CorchEdges.Data.Abstractions;
using Npgsql;

namespace CorchEdges.Data;

/// <summary>
/// Provides functionality for writing multiple tables, encapsulated within a <see cref="DataSet"/>,
/// to a PostgreSQL database using binary COPY commands for efficient bulk insertion.
/// </summary>
/// <remarks>
/// This class is specifically designed to handle bulk insertion scenarios where multiple
/// tables need to be written to a PostgreSQL database. It utilizes the COPY mechanism
/// provided by PostgreSQL for high-performance data transfer.
/// </remarks>
public sealed class PostgresTableWriter : IPostgresTableWriter
{
    /// <summary>
    /// Asynchronously writes a collection of tables, encapsulated within a <see cref="DataSet"/>,
    /// to a PostgreSQL database using the specified database connection and transaction.
    /// Each table is written via PostgreSQL's binary COPY functionality to enhance speed and efficiency.
    /// </summary>
    /// <param name="tables">The <see cref="DataSet"/> containing the tables to be written to the database.</param>
    /// <param name="connection">The <see cref="DbConnection"/> to the PostgreSQL database where the data will be written.</param>
    /// <param name="transaction">The <see cref="DbTransaction"/> that ensures data consistency during the write operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous write operation.</returns>
    public async Task WriteAsync(DataSet tables, DbConnection connection, DbTransaction transaction)
    {
        var c = (NpgsqlConnection)connection;
        
        foreach (DataTable tbl in tables.Tables)
        {
            if (tbl.Rows.Count == 0) continue;

            var cols = string.Join(", ", tbl.Columns.Cast<DataColumn>().Select(col => $"\"{col.ColumnName}\""));
            
            // Validate and clean a table name BEFORE using it in SQL
            var tableName = CleanTableName(tbl.TableName);
            
            await using var writer =
                await c.BeginBinaryImportAsync($"COPY {tableName} ({cols}) FROM STDIN BINARY");
            
            foreach (DataRow row in tbl.Rows)
            {
                await writer.WriteRowAsync(CancellationToken.None, row.ItemArray);
            }
            
            await writer.CompleteAsync();
        }
    }

    /// <summary>
    /// Cleans and validates a Postgres table name for safe usage in SQL queries.
    /// Ensures the table name is properly escaped and formatted, optionally including a schema name.
    /// </summary>
    /// <param name="tableName">The original table name that may include schema and table parts.</param>
    /// <returns>A properly cleaned and escaped table name to be used in SQL commands.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the provided table name is invalid, such as being empty, improperly formatted,
    /// or lacking necessary schema or table definitions.
    /// </exception>
    private static string CleanTableName(string tableName)
    {
        var parts = tableName.Split('.');
        
        if (parts.Length == 1)
        {
            var cleanName = parts[0].Trim('"');
            if (string.IsNullOrEmpty(cleanName)) 
                throw new ArgumentException("Empty table name");
            return $"\"{cleanName}\"";
        }
        else if (parts.Length == 2)
        {
            var schema = parts[0].Trim('"');
            var table = parts[1].Trim('"');
            
            if (string.IsNullOrEmpty(schema)) 
                throw new ArgumentException("Empty schema name");
            if (string.IsNullOrEmpty(table)) 
                throw new ArgumentException("Empty table name");
            
            return $"\"{schema}\".\"{table}\"";
        }
        else
        {
            throw new ArgumentException($"Invalid table name format: {tableName}");
        }
    }
}