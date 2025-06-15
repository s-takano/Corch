using CorchEdges.Data;
using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace CorchEdges.Tests;

public abstract class DatabaseTestBase : IDisposable
{
    private string TestSchema { get; set; }
    private string ConnectionString { get; set; }
    
    // Shared connection for all tests in this class
    protected internal NpgsqlConnection Connection { get; private set; } = null!;

    protected DatabaseTestBase()
    {
        Env.Load();
        
        // Generate a unique schema name for this test run
        TestSchema = $"test_{Guid.NewGuid().ToString("N")}";
        ConnectionString = GetConnectionString();
        
        SetupTestSchema();
        InitializeConnection();
    }
    
    // Method to create a fresh DbContext for each test
    protected EdgesDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<EdgesDbContext>()
            .UseNpgsql(Connection) // Use the shared connection directly
            .Options;
            
        var context = new EdgesDbContext(options);
        
        // Configure the context to use our test schema by default
        context.Database.ExecuteSql($"SET search_path TO \"{TestSchema}\"");
        
        return context;
    }
    
    private void InitializeConnection()
    {
        Connection = new NpgsqlConnection(ConnectionString);
        Connection.Open();
        
        // Set the search path to use our test schema by default
        using var cmd = new NpgsqlCommand($"SET search_path TO \"{TestSchema}\"", Connection);
        cmd.ExecuteNonQuery();
    }
    
    private string GetConnectionString()
    {
        // Priority order: Environment Variable -> User Secrets
        
        // Option 1: Environment variable (for CI/CD)
        var envConnection = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION");
        if (!string.IsNullOrEmpty(envConnection))
            return envConnection;

        // Option 2: User Secrets
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .AddUserSecrets<DatabaseTestBase>()
            .Build();
            
        var configConnection = configuration.GetConnectionString("TestDatabase");
        if (!string.IsNullOrEmpty(configConnection))
            return configConnection;

        throw new InvalidOperationException(
            "No test database connection string found. Please set the connection string using: '.env' set \"ConnectionStrings__TestDatabase\" \"your-connection-string\"");
    }
    
    private void SetupTestSchema()
    {
        using var connection = new NpgsqlConnection(ConnectionString);
        connection.Open();
        
        // Create schema
        using var cmd = new NpgsqlCommand($"CREATE SCHEMA \"{TestSchema}\"", connection);
        cmd.ExecuteNonQuery();
        
        // Run EF migrations for this schema
        using var context = CreateDbContextForMigration();
        context.Database.Migrate();
    }

    // Separate method for migration that creates its own connection
    private EdgesDbContext CreateDbContextForMigration() 
    {
        var options = new DbContextOptionsBuilder<EdgesDbContext>()
            .UseNpgsql(ConnectionString, npgsqlOptions =>
            {
                // Set the migration history table to use our test schema
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", TestSchema);
            })
            .Options;
            
        var context = new EdgesDbContext(options);
        
        // Configure the context to use our test schema by default
        context.Database.ExecuteSql($"SET search_path TO \"{TestSchema}\"");
        
        return context;
    }

    protected async Task<string> SetupTestTable(string tableName, string tableDefinition)
    {
        var qualifiedTableName = $"\"{TestSchema}\".\"{tableName}\"";

        var createTableSql = $"CREATE TABLE {qualifiedTableName} ({tableDefinition})";
        await using var cmd = new NpgsqlCommand(createTableSql, Connection);
        await cmd.ExecuteNonQueryAsync();

        return qualifiedTableName;
    }

    // Helper for getting table data (useful for assertions)
    protected async Task<int> GetTableRowCount(string tableName)
    {
        await using var cmd = new NpgsqlCommand($"SELECT COUNT(*) FROM {tableName}", Connection);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    protected async Task<List<Dictionary<string, object>>> GetTableData(string tableName)
    {
        await using var cmd = new NpgsqlCommand($"SELECT * FROM {tableName}", Connection);
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
        
        // Cleanup: Drop the test schema and all its objects
        try
        {
            using var connection = new NpgsqlConnection(ConnectionString);
            connection.Open();
            using var cmd = new NpgsqlCommand($"DROP SCHEMA IF EXISTS \"{TestSchema}\" CASCADE", connection);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            // Log but don't throw - cleanup shouldn't break tests
            Console.WriteLine($"Warning: Failed to cleanup test schema {TestSchema}: {ex.Message}");
        }
    }
}