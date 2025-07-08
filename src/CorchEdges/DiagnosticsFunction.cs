using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using CorchEdges.Abstractions;
using CorchEdges.Data;
using CorchEdges.Data.Abstractions;
using Npgsql;

namespace CorchEdges;

public class DiagnosticsFunction
{
    private readonly ILogger<DiagnosticsFunction> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;

    public DiagnosticsFunction(
        ILogger<DiagnosticsFunction> logger,
        IConfiguration configuration,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _configuration = configuration;
        _serviceProvider = serviceProvider;
    }

    [Function("Diagnostics")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get")]
        HttpRequestData req)
    {
        _logger.LogInformation("Diagnostics function executed.");

        var diagnostics = new
        {
            Environment = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT") ?? "Unknown",
            ConfigurationSources = GetConfigurationSources(),
            ConnectionStrings = GetConnectionStrings(),
            DatabaseConnection = GetDatabaseConnectionStatus(),
            AppSettings = GetAppSettings(),
            ServiceRegistrations = GetServiceRegistrationStatus(),
            EnvironmentVariables = GetRelevantEnvironmentVariables()
        };

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");

        var json = JsonSerializer.Serialize(diagnostics, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await response.WriteStringAsync(json);
        return response;
    }

    private object GetDatabaseConnectionStatus()
    {
        var connectionString = _configuration.GetConnectionString("PostgreSQLConnection");
        return connectionString == null ? DatabaseConnectionResult.Failure("Database not configured", "PostgreSQL connection string missing") : TestDatabaseConnectionDetailed(connectionString);
    }

    static DatabaseConnectionResult TestDatabaseConnectionDetailed(string connectionString)
    {
        try
        {
            using var connection = new NpgsqlConnection(connectionString);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            connection.Open();
            stopwatch.Stop();

            // Get server information
            var serverVersion = connection.ServerVersion;
            var database = connection.Database;
            var host = connection.Host;
            var port = connection.Port;

            // Test a simple query
            using var command = new NpgsqlCommand("SELECT version(), current_database(), current_user", connection);
            using var reader = command.ExecuteReader();

            string? versionInfo = null;
            string? currentUser = null;

            if (reader.Read())
            {
                versionInfo = reader.GetString(0);
                currentUser = reader.GetString(2);
            }

            return DatabaseConnectionResult.Success(
                $"PostgreSQL {serverVersion} on {host}:{port}",
                $"Connected to '{database}' as '{currentUser}' in {stopwatch.ElapsedMilliseconds}ms",
                versionInfo);
        }
        catch (NpgsqlException npgsqlEx)
        {
            return DatabaseConnectionResult.Failure(
                "PostgreSQL connection error",
                GetPostgresErrorDetails(npgsqlEx));
        }
        catch (Exception ex)
        {
            return DatabaseConnectionResult.Failure(
                "Unexpected connection error",
                $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    static string GetPostgresErrorDetails(NpgsqlException ex)
    {
        return ex.SqlState switch
        {
            "28P01" => "Authentication failed - check username/password",
            "3D000" => "Database does not exist",
            "28000" => "Invalid authorization specification",
            "08001" => "Unable to connect to server - check host/port",
            "08006" => "Connection failure - server may be down",
            _ => $"PostgreSQL Error [{ex.SqlState}]: {ex.Message}"
        };
    }

    record DatabaseConnectionResult(
        bool IsSuccess,
        string? ErrorMessage = null,
        string? Details = null,
        string? ServerInfo = null)
    {
        public static DatabaseConnectionResult Success(string serverInfo, string details, string? versionInfo = null) =>
            new(true, ServerInfo: serverInfo, Details: details);

        public static DatabaseConnectionResult Failure(string errorMessage, string details) =>
            new(false, errorMessage, details);
    }

    private object GetConfigurationSources()
    {
        if (_configuration is IConfigurationRoot root)
        {
            return root.Providers.Select(p => new
            {
                Type = p.GetType().Name,
                Keys = p.GetChildKeys(Enumerable.Empty<string>(), null).Take(10).ToList()
            }).ToList();
        }

        return new { Error = "Configuration is not IConfigurationRoot" };
    }

    private object GetConnectionStrings()
    {
        return new
        {
            PostgreSQLConnection = _configuration.GetConnectionString("PostgreSQLConnection") != null
                ? "CONFIGURED"
                : "NOT FOUND",
            ServiceBusConnection = _configuration.GetConnectionString("ServiceBusConnection") != null
                ? "CONFIGURED"
                : "NOT FOUND"
        };
    }

    private object GetAppSettings()
    {
        return new
        {
            SharePointSiteId = _configuration["SharePoint:SiteId"] ?? "NOT FOUND",
            SharePointListId = _configuration["SharePoint:ListId"] ?? "NOT FOUND",
            AzureFunctionsEnvironment = _configuration["AZURE_FUNCTIONS_ENVIRONMENT"] ?? "NOT FOUND"
        };
    }

    private IDictionary<string, string> GetServiceRegistrationStatus()
    {
        var services = new Dictionary<string, string>();

        try
        {
            var dbContext = _serviceProvider.GetService<EdgesDbContext>();
            services["EdgesDbContext"] = dbContext != null ? "✓ REGISTERED" : "✗ NOT REGISTERED";
        }
        catch (Exception ex)
        {
            services["EdgesDbContext"] = $"✗ ERROR: {ex.Message}";
        }

        try
        {
            var excelParser = _serviceProvider.GetService<IExcelParser>();
            services["IExcelParser"] = excelParser != null ? "✓ REGISTERED" : "✗ NOT REGISTERED";
        }
        catch (Exception ex)
        {
            services["IExcelParser"] = $"✗ ERROR: {ex.Message}";
        }

        try
        {
            var dbWriter = _serviceProvider.GetService<IDatabaseWriter>();
            services["IDatabaseWriter"] = dbWriter != null ? "✓ REGISTERED" : "✗ NOT REGISTERED";
        }
        catch (Exception ex)
        {
            services["IDatabaseWriter"] = $"✗ ERROR: {ex.Message}";
        }

        return services;
    }

    private object GetRelevantEnvironmentVariables()
    {
        var envVars = new Dictionary<string, string>();

        var relevantKeys = new[]
        {
            "AZURE_FUNCTIONS_ENVIRONMENT",
            "WEBSITE_SITE_NAME",
            "AZURE_CLIENT_ID",
            "AZURE_TENANT_ID",
            "ConnectionStrings__ServiceBusConnection",
            "ConnectionStrings__PostgreSQLConnection"
        };

        foreach (var key in relevantKeys)
        {
            var value = Environment.GetEnvironmentVariable(key);
            envVars[key] = value != null ? "SET" : "NOT SET";
        }

        return envVars;
    }
}