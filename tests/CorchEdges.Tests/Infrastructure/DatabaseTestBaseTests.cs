
using Npgsql;
using Xunit;

namespace CorchEdges.Tests
{
    /// <summary>
    /// Tests the DatabaseTestBase infrastructure itself
    /// </summary>
    public class DatabaseTestBaseTests : DatabaseTestBase
    {
        [Fact]
        public void Constructor_InitializesConnection_ConnectionIsOpen()
        {
            // Act & Assert
            Assert.NotNull(Connection);
            Assert.Equal(System.Data.ConnectionState.Open, Connection.State);
        }

        [Fact]
        public void Constructor_SetsUpUniqueSchema_SchemaExists()
        {
            // Arrange & Act - schema setup happens in constructor
            
            // Assert - verify we can query schema information
            using var cmd = new NpgsqlCommand(
                "SELECT schema_name FROM information_schema.schemata WHERE schema_name LIKE 'test_%'", 
                Connection);
            
            using var reader = cmd.ExecuteReader();
            var schemas = new List<string>();
            while (reader.Read())
            {
                schemas.Add(reader.GetString(0));
            }

            Assert.Contains(schemas, schema => schema.StartsWith("test_"));
        }

        [Fact]
        public async Task SetupTestTable_CreatesTable_TableExistsAndIsAccessible()
        {
            // Arrange
            var tableName = "test_users";
            var tableDefinition = "id INTEGER PRIMARY KEY, name VARCHAR(100) NOT NULL";

            // Act
            var qualifiedTableName = await SetupTestTable(tableName, tableDefinition);

            // Assert
            Assert.StartsWith("\"test_", qualifiedTableName);
            Assert.Contains($"\".\"{ tableName}\"", qualifiedTableName);

            // Verify table exists by inserting and querying data
            await using var insertCmd = new NpgsqlCommand(
                $"INSERT INTO {qualifiedTableName} (id, name) VALUES (1, 'Test User')", 
                Connection);
            await insertCmd.ExecuteNonQueryAsync();

            var count = await GetTableRowCount(qualifiedTableName);
            Assert.Equal(1, count);
        }

        [Fact]
        public async Task GetTableRowCount_WithData_ReturnsCorrectCount()
        {
            // Arrange
            var tableName = await SetupTestTable("count_test", "id INTEGER");
            
            // Insert test data
            for (int i = 1; i <= 3; i++)
            {
                await using var cmd = new NpgsqlCommand($"INSERT INTO {tableName} (id) VALUES ({i})", Connection);
                await cmd.ExecuteNonQueryAsync();
            }

            // Act
            var count = await GetTableRowCount(tableName);

            // Assert
            Assert.Equal(3, count);
        }

        [Fact]
        public async Task GetTableData_WithData_ReturnsAllRows()
        {
            // Arrange
            var tableName = await SetupTestTable("data_test", "id INTEGER, name VARCHAR(50)");
            
            await using var insertCmd = new NpgsqlCommand(
                $"INSERT INTO {tableName} (id, name) VALUES (1, 'Alice'), (2, 'Bob')", 
                Connection);
            await insertCmd.ExecuteNonQueryAsync();

            // Act
            var data = await GetTableData(tableName);

            // Assert
            Assert.Equal(2, data.Count);
            
            var firstRow = data[0];
            Assert.Equal(1, firstRow["id"]);
            Assert.Equal("Alice", firstRow["name"]);

            var secondRow = data[1];
            Assert.Equal(2, secondRow["id"]);
            Assert.Equal("Bob", secondRow["name"]);
        }

        [Fact]
        public async Task MultipleTestTables_InSameTest_BothAccessible()
        {
            // Arrange & Act
            var usersTable = await SetupTestTable("users", "id INTEGER, name VARCHAR(100)");
            var ordersTable = await SetupTestTable("orders", "id INTEGER, user_id INTEGER");

            // Insert data into both tables
            await using var userCmd = new NpgsqlCommand($"INSERT INTO {usersTable} (id, name) VALUES (1, 'John')", Connection);
            await userCmd.ExecuteNonQueryAsync();

            await using var orderCmd = new NpgsqlCommand($"INSERT INTO {ordersTable} (id, user_id) VALUES (1, 1)", Connection);
            await orderCmd.ExecuteNonQueryAsync();

            // Assert
            Assert.Equal(1, await GetTableRowCount(usersTable));
            Assert.Equal(1, await GetTableRowCount(ordersTable));
        }

        [Fact]
        public void Connection_UsesTestSchema_QueriesExecuteInCorrectSchema()
        {
            // Arrange & Act
            using var cmd = new NpgsqlCommand("SELECT current_schema()", Connection);
            var currentSchema = cmd.ExecuteScalar()?.ToString();

            // Assert
            Assert.NotNull(currentSchema);
            Assert.StartsWith("test_", currentSchema);
        }

        [Fact]
        public async Task TestTransaction_RollsBackCorrectly()
        {
            // Arrange
            var tableName = await SetupTestTable("transaction_test", "id INTEGER");
            
            // Act
            await using var transaction = await Connection.BeginTransactionAsync();
            
            await using var insertCmd = new NpgsqlCommand($"INSERT INTO {tableName} (id) VALUES (1)", Connection, transaction);
            await insertCmd.ExecuteNonQueryAsync();
            
            // Verify data exists within transaction
            await using var checkCmd = new NpgsqlCommand($"SELECT COUNT(*) FROM {tableName}", Connection, transaction);
            var countInTransaction = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
            Assert.Equal(1, countInTransaction);
            
            await transaction.RollbackAsync();

            // Assert - data should be gone after rollback
            var countAfterRollback = await GetTableRowCount(tableName);
            Assert.Equal(0, countAfterRollback);
        }
    }
}