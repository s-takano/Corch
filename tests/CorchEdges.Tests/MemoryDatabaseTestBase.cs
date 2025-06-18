using System.Data.Common;
using CorchEdges.Data;
using DotNetEnv;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace CorchEdges.Tests;

public abstract class MemoryDatabaseTestBase : IDisposable
{
    private string ConnectionString { get; set; } = "DataSource=:memory:";
    
    // Shared connection for all tests in this class
    protected internal SqliteConnection Connection { get; private set; } = null!;

    protected MemoryDatabaseTestBase()
    {
        Env.Load();

        InitializeConnection();
    }

    public static EdgesDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<EdgesDbContext>()
            .UseSqlite("DataSource=:memory:")
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.AmbientTransactionWarning))
            .Options;

        var context = new EdgesDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }

    private void InitializeConnection()
    {
        Connection = new SqliteConnection(ConnectionString);
        Connection.Open();
    }
    

    protected async Task<string> SetupTestTable(string tableName, string tableDefinition)
    {
        var createTableSql = $"CREATE TABLE {tableName} ({tableDefinition})";
        await using var cmd = new SqliteCommand(createTableSql, Connection);
        await cmd.ExecuteNonQueryAsync();
    
        return tableName;
    }

    // Helper for getting table data (useful for assertions)
    protected async Task<int> GetTableRowCount(string tableName)
    {
        await using var cmd = new SqliteCommand($"SELECT COUNT(*) FROM {tableName}", Connection);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }
    
    protected async Task<List<Dictionary<string, object>>> GetTableData(string tableName)
    {
        await using var cmd = new SqliteCommand($"SELECT * FROM {tableName}", Connection);
        await using var reader = await cmd.ExecuteReaderAsync();
        
        var results = new List<Dictionary<string, object>>();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.GetValue(i);
            }
            results.Add(row);
        }
        return results;
    }
    
    public void Dispose()
    {
        // Close connection
        Connection?.Dispose();
    }
}