using Npgsql;

namespace CorchEdges.Tests.Infrastructure
{
    public class DatabaseSchemaIsolationTests : IDisposable
    {
        private readonly List<MemoryDatabaseTestBase> _testInstances = new();


        public void Dispose()
        {
            foreach (var instance in _testInstances)
            {
                instance.Dispose();
            }
        }

        // Simple concrete implementation for testing
        private class TestInstance : MemoryDatabaseTestBase
        {
            // Intentionally empty - we just need a concrete class
        }
    }
}