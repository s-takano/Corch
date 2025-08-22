using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace CorchEdges.Data
{
    /// <summary>
    /// A factory for creating instances of <see cref="EdgesDbContext"/> during design-time.
    /// </summary>
    /// <remarks>
    /// This class implements the <see cref="IDesignTimeDbContextFactory{TContext}"/> interface,
    /// allowing the Entity Framework tools to create a database context instance for migration purposes.
    /// It configures the context by reading connection settings from environment-specific configuration files and environment variables.
    /// The factory also supports additional optional configuration, such as using Key Vaults and enabling sensitive data logging in development environments.
    /// </remarks>
    public sealed class EdgesDbContextFactory : IDesignTimeDbContextFactory<EdgesDbContext>
    {
        public EdgesDbContext CreateDbContext(string[] args)
        {
            var basePath = AppContext.BaseDirectory;

            // Match environment logic used by the Functions host
            var env =
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                ?? Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT")
                ?? "Development";

            // Build configuration like Program.cs
            var cfgBuilder = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile($"appsettings.{env}.json", optional: true)
                .AddEnvironmentVariables()
                .AddUserSecrets<EdgesDbContextFactory>();

            var config = cfgBuilder.Build();

            // Use the same key as in Program.cs
            var connectionString = config.GetConnectionString("PostgreSQLConnection");
            if (string.IsNullOrEmpty(connectionString))
                throw new InvalidOperationException("Database not configured - PostgreSQL connection string missing");

            var optionsBuilder = new DbContextOptionsBuilder<EdgesDbContext>()
                .UseNpgsql(connectionString,             
                    // Keep EF's migration history isolated per schema
                    npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "corch_edges_raw")
                );

            // Optional: helpful when developing locally
            if (string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase))
                optionsBuilder.EnableSensitiveDataLogging();

            return new EdgesDbContext(optionsBuilder.Options);
        }
    }
}
