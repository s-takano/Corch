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
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        _logger.LogInformation("Diagnostics function executed.");

        var diagnostics = new
        {
            Environment = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT") ?? "Unknown",
            ConfigurationSources = GetConfigurationSources(),
            ConnectionStrings = GetConnectionStrings(),
            AppSettings = GetAppSettings(),
            ServiceRegistrations = await GetServiceRegistrationStatus(),
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
            PostgreSQLConnection = _configuration.GetConnectionString("PostgreSQLConnection") != null ? "CONFIGURED" : "NOT FOUND",
            ServiceBusConnection = _configuration.GetConnectionString("ServiceBusConnection") != null ? "CONFIGURED" : "NOT FOUND"
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

    private async Task<object> GetServiceRegistrationStatus()
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