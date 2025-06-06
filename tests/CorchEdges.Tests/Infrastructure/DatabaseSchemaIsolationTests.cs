using Npgsql;

namespace CorchEdges.Tests.Infrastructure
{
    public class DatabaseSchemaIsolationTests : IDisposable
    {
        private readonly List<DatabaseTestBase> _testInstances = new();

        [Fact]
        public void MultipleInstances_GetDifferentSchemas()
        {
            // Arrange & Act
            var instance1 = new TestInstance();
            var instance2 = new TestInstance();
            
            _testInstances.Add(instance1);
            _testInstances.Add(instance2);

            // Assert
            using var cmd1 = new NpgsqlCommand("SELECT current_schema()", instance1.Connection);
            using var cmd2 = new NpgsqlCommand("SELECT current_schema()", instance2.Connection);
            
            var schema1 = cmd1.ExecuteScalar()?.ToString();
            var schema2 = cmd2.ExecuteScalar()?.ToString();

            Assert.NotEqual(schema1, schema2);
            Assert.StartsWith("test_", schema1);
            Assert.StartsWith("test_", schema2);
        }

        public void Dispose()
        {
            foreach (var instance in _testInstances)
            {
                instance.Dispose();
            }
        }

        // Simple concrete implementation for testing
        private class TestInstance : DatabaseTestBase
        {
            // Intentionally empty - we just need a concrete class
        }
    }
}