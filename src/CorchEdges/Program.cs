using System.Data;
using Azure.Core;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Storage.Blobs;
using CorchEdges;
using CorchEdges.Abstractions;
using CorchEdges.Data;
using CorchEdges.Data.Abstractions;
using CorchEdges.Data.Repositories;
using CorchEdges.Services;
using CorchEdges.Utilities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;

Console.WriteLine("[Bootstrap] CorchEdges Functions starting…");

var host = Host.CreateDefaultBuilder(args)

// ───────────────────────────────────────── Functions runtime
    .ConfigureFunctionsWorkerDefaults(worker =>
    {
        // Emit early log (console) – ILogger not ready yet
        Console.WriteLine("[Bootstrap] Configuring Functions worker defaults");
        worker.Services.Configure<WorkerOptions>(o => o.EnableUserCodeException = true);
    })

// ───────────────────────────────────────── Configuration
    .ConfigureAppConfiguration((ctx, cfg) =>
    {
        cfg.AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{ctx.HostingEnvironment.EnvironmentName}.json", optional: true)
            .AddEnvironmentVariables();

        AddKeyVault(cfg, ctx.HostingEnvironment.EnvironmentName);
    })

// ───────────────────────────────────────── Logging
    .ConfigureLogging((ctx, log) =>
    {
        // Add the extra sinks you want
        log.AddConsole();

        // Narrow noise with filters instead of ClearProviders
        log.SetMinimumLevel(LogLevel.Warning); // default for everything
        log.AddFilter("Startup", LogLevel.Information); // your Program.cs
        log.AddFilter("CorchEdges", LogLevel.Debug); // your functions
    })

// ───────────────────────────────────────── DI registrations
    .ConfigureServices((ctx, svcs) =>
    {
        svcs.AddApplicationInsightsTelemetryWorkerService();
        svcs.ConfigureFunctionsApplicationInsights();

        // Register BlobServiceClient
        svcs.AddSingleton<BlobServiceClient>(provider =>
        {
            var blobServiceUri = Environment.GetEnvironmentVariable("AzureWebJobsStorage__blobServiceUri")
                                 ?? throw new InvalidOperationException(
                                     "AzureWebJobsStorage__blobServiceUri connection string is missing");

            var credential = provider.GetRequiredService<TokenCredential>();

            return new BlobServiceClient(new Uri(blobServiceUri), credential);
        });

        svcs.AddScoped<IWebhookProcessor, DefaultWebhookProcessor>();

        RegisterDatabaseServices(svcs, ctx.Configuration);
        RegisterGraph(svcs);
        RegisterServiceBus(svcs, ctx.Configuration);
        RegisterBusiness(svcs, ctx.Configuration);
    })
    .Build();

// ───────────────────────────────────────── Lifetime diagnostics
var logger = host.Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger("Startup");
logger.LogInformation("CorchEdges host built for environment: {Env}",
    host.Services.GetRequiredService<IHostEnvironment>().EnvironmentName);

var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

lifetime.ApplicationStarted.Register(() =>
    logger.LogInformation("Host STARTED  {Time:u}", DateTime.UtcNow));

lifetime.ApplicationStopping.Register(() =>
    logger.LogWarning("Host STOPPING {Time:u}", DateTime.UtcNow));

lifetime.ApplicationStopped.Register(() =>
    logger.LogInformation("Host STOPPED  {Time:u}", DateTime.UtcNow));

// ───────────────────────────────────────── Run!
await host.RunAsync();


// ───────────────────────────────────────── helper methods
static void AddKeyVault(IConfigurationBuilder cfg, string envName)
{
    var kv = envName.ToLower() switch
    {
        "development" => "corch-edges-dev-kv",
        "test" => "corch-edges-test-kv",
        "staging" => "corch-edges-staging-kv",
        _ => "corch-edges-prod-kv"
    };

    Console.WriteLine($"[Bootstrap] Adding Key Vault: {kv}");
    cfg.AddAzureKeyVault(new Uri($"https://{kv}.vault.azure.net/"),
        new DefaultAzureCredential());
}

static void RegisterDatabaseServices(IServiceCollection services, IConfiguration config)
{
    Console.WriteLine("Starting database services registration...");

    services.AddDbContext<EdgesDbContext>(CreateContextFactory(config));
    
    services.AddTransient<PostgresTableWriter>();
    services.AddTransient<ExcelDatasetWriter>(provider => new ExcelDatasetWriter(
        provider.GetRequiredService<IPostgresTableWriter>(),
        provider.GetRequiredService<ILogger<ExcelDatasetWriter>>()));

    services.AddTransient<ProcessingLogRepository>();
    
    Console.WriteLine("Database services registration completed.");
}

static Action<DbContextOptionsBuilder> CreateContextFactory(IConfiguration config)
{
    var connectionString = config.GetConnectionString("PostgreSQLConnection");
    if (string.IsNullOrEmpty(connectionString))
    {
        // Register stub implementations when a database is not available
        throw new InvalidOperationException("Database not configured - PostgreSQL connection string missing");
    }

    Console.WriteLine("✓ Database services registered with a valid connection string");

    return options => { options.UseNpgsql(connectionString); };
}

static void RegisterGraph(IServiceCollection svcs)
{
    Console.WriteLine("Starting Graph services registration...");

    // 1️⃣  one TokenCredential to rule them all
    svcs.AddSingleton<TokenCredential, DefaultAzureCredential>();

    // 2️⃣  one GraphServiceClient, singleton, with resource scope
    svcs.AddSingleton<GraphServiceClient>(sp =>
    {
        var credential = sp.GetRequiredService<TokenCredential>();
        var scopes = new[] { "https://graph.microsoft.com/.default" };
        return new GraphServiceClient(credential, scopes);
    });

    svcs.AddTransient<IGraphFacade, GraphFacade>();

    Console.WriteLine("✓ Graph services registration completed.");
}


static void RegisterServiceBus(IServiceCollection svcs, IConfiguration cfg)
{
    Console.WriteLine("Starting Service Bus registration...");

    svcs.AddSingleton<ServiceBusClient>(_ =>
    {
        var cs = cfg.GetConnectionString("ServiceBusConnection");
        if (!string.IsNullOrEmpty(cs))
        {
            Console.WriteLine("✓ Service Bus registered with connection string");
            return new ServiceBusClient(cs);
        }

        var env = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT")?.ToLower() ?? "production";
        var fqns = $"corch-edges-{env}-sb1.servicebus.windows.net";
        Console.WriteLine($"✓ Service Bus registered with FQNS: {fqns}");
        return new ServiceBusClient(fqns, new DefaultAzureCredential());
    });

    Console.WriteLine("Service Bus registration completed.");
}


static void RegisterBusiness(IServiceCollection svcs, IConfiguration cfg)
{
    Console.WriteLine("Starting Business services registration...");

    svcs.AddTransient<IExcelParser, ExcelDataParser>();
    svcs.AddTransient<IDatabaseWriter, ExcelDatasetWriter>();

    // Add WebhookRegistrationService service
    svcs.AddTransient<WebhookRegistration>();

    svcs.AddScoped<SharePointChangeHandler>(p => new SharePointChangeHandler(
        p.GetRequiredService<ILogger<SharePointChangeHandler>>(),
        p.GetRequiredService<IGraphFacade>(),
        p.GetRequiredService<IExcelParser>(),
        p.GetRequiredService<IDatabaseWriter>(),
        p.GetRequiredService<EdgesDbContext>(),
        p.GetRequiredService<ProcessingLogRepository>(),
        cfg["SharePoint:SiteId"] ?? "MISSING",
        cfg["SharePoint:ListId"] ?? "MISSING",
        cfg["SharePoint:WatchedPath"] ?? "MISSING"));

    var siteId = cfg["SharePoint:SiteId"] ?? "MISSING";
    var listId = cfg["SharePoint:ListId"] ?? "MISSING";
    Console.WriteLine($"✓ Business services registration completed. SharePoint SiteId: {siteId}, ListId: {listId}");
}