using System.Data;
using System.Data.Common;
using Npgsql;

namespace CorchEdges.Data;

public interface IPostgresTableWriter
{
    Task WriteAsync(DataSet tables, DbConnection connection, DbTransaction transaction);
}

public sealed class PostgresTableWriter : IPostgresTableWriter
{
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