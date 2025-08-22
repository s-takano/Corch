using CorchEdges.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace CorchEdges.Tests.Infrastructure;

public abstract class PostgresDatabaseTestBase : IDisposable
{
    private string TestDatabase { get; set; }
    private string MasterConnectionString { get; set; }
    private string TestConnectionString { get; set; }
    
    // Fixed schema name as required
    protected virtual string TestSchema => "corch_edges_raw_test";
    
    // Shared connection for all tests in this class
    protected internal NpgsqlConnection Connection { get; private set; } = null!;

    protected PostgresDatabaseTestBase()
    {
        // Generate a unique database name for this test class
        TestDatabase = $"testdb_{Guid.NewGuid():N}";
        
        // Get the base connection string and create master/test variants
        var baseConnectionString = GetBaseConnectionString();
        MasterConnectionString = CreateMasterConnectionString(baseConnectionString);
        TestConnectionString = CreateTestConnectionString(baseConnectionString);
        
        SetupTestDatabase();
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
        Connection = new NpgsqlConnection(TestConnectionString);
        Connection.Open();
        
        // Set the search path to use our test schema by default
        using var cmd = new NpgsqlCommand($"SET search_path TO \"{TestSchema}\"", Connection);
        cmd.ExecuteNonQuery();
    }

    private string GetBaseConnectionString()
    {
        // Priority order: Environment Variable -> User Secrets
        
        // Option 1: Environment variable (for CI/CD)
        var envConnection = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION");
        if (!string.IsNullOrEmpty(envConnection))
            return envConnection;

        // Option 2: User Secrets
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<PostgresDatabaseTestBase>()
            .Build();
            
        var configConnection = configuration.GetConnectionString("TestDatabase");
        if (!string.IsNullOrEmpty(configConnection))
            return configConnection;

        throw new InvalidOperationException(
            "No test database connection string found. Please set the connection string using: dotnet user-secrets set \"ConnectionStrings:TestDatabase\" \"your-connection-string\"");
    }

    private string CreateMasterConnectionString(string baseConnectionString)
    {
        // Parse the base connection string and modify it to connect to the master database
        var builder = new NpgsqlConnectionStringBuilder(baseConnectionString);
        
        // Connect to 'postgres' database (the default master database)
        builder.Database = "postgres";
        
        return builder.ToString();
    }

    private string CreateTestConnectionString(string baseConnectionString)
    {
        // Parse the base connection string and modify it to connect to our test database
        var builder = new NpgsqlConnectionStringBuilder(baseConnectionString);
        
        // Connect to our unique test database
        builder.Database = TestDatabase;
        
        return builder.ToString();
    }

    private void SetupTestDatabase()
    {
        // Create the test database using master connection
        using var connection = new NpgsqlConnection(MasterConnectionString);
        connection.Open();
        
        using var cmd = new NpgsqlCommand($"CREATE DATABASE \"{TestDatabase}\"", connection);
        cmd.ExecuteNonQuery();
        
        // Now create the schema and run migrations in the test database
        using var testConnection = new NpgsqlConnection(TestConnectionString);
        testConnection.Open();
        
        // Create the fixed schema
        using var schemaCmd = new NpgsqlCommand($"CREATE SCHEMA \"{TestSchema}\"", testConnection);
        schemaCmd.ExecuteNonQuery();
        
        // Run EF migrations for this schema
        using var context = CreateDbContextForMigration();
        context.Database.Migrate();
    }

    // Separate method for migration that creates its own connection
    private EdgesDbContext CreateDbContextForMigration() 
    {
        var options = new DbContextOptionsBuilder<EdgesDbContext>()
            .UseNpgsql(TestConnectionString, npgsqlOptions =>
            {
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
        var qualifiedTableName = tableName;
        
        var createTableSql = $"CREATE TABLE {qualifiedTableName} ({tableDefinition})";
        await using var cmd = new NpgsqlCommand(createTableSql, Connection);
        await cmd.ExecuteNonQueryAsync();

        return qualifiedTableName;
    }

    // Helper method specifically for CreateDatabaseTablesFromDataSet
    protected async Task<string> SetupTestTableFromMappedName(string mappedTableName, string tableDefinition)
    {
        // mappedTableName comes as "corch_edges_raw.table_name" format
        var parts = mappedTableName.Split('.');
        if (parts.Length != 2)
        {
            throw new ArgumentException($"Expected schema.table format, got: {mappedTableName}");
        }

        var originalSchema = parts[0];
        var tableName = parts[1];
        
        // Create table in our fixed test schema (corch_edges_raw)
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
        // Close connection to test database
        Connection?.Dispose();
        
        // Drop the test database using master connection
        try
        {
            using var connection = new NpgsqlConnection(MasterConnectionString);
            connection.Open();
            
            // Terminate any remaining connections to the test database
            using var terminateCmd = new NpgsqlCommand($@"
                SELECT pg_terminate_backend(pg_stat_activity.pid)
                FROM pg_stat_activity
                WHERE pg_stat_activity.datname = '{TestDatabase}'
                  AND pid <> pg_backend_pid()", connection);
            terminateCmd.ExecuteNonQuery();
            
            // Drop the test database
            using var dropCmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{TestDatabase}\"", connection);
            dropCmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            // Log but don't throw - cleanup shouldn't break tests
            Console.WriteLine($"Warning: Failed to cleanup test database {TestDatabase}: {ex.Message}");
        }
    }
}