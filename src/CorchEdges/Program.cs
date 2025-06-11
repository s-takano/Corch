using System.Data;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using CorchEdges;
using CorchEdges.Abstractions;
using CorchEdges.Data;
using CorchEdges.Data.Abstractions;
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
        log.SetMinimumLevel(LogLevel.Warning);              // default for everything
        log.AddFilter("Startup",     LogLevel.Information); // your Program.cs
        log.AddFilter("CorchEdges",  LogLevel.Debug);       // your functions
    })

// ───────────────────────────────────────── DI registrations
    .ConfigureServices((ctx, svcs) =>
    {
        svcs.AddApplicationInsightsTelemetryWorkerService();
        svcs.ConfigureFunctionsApplicationInsights();

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
    
    var (dbContextFactory, postgresTableWriterFactory, databaseWriterFactory) = CreateDatabaseFactories(config);
    
    services.AddDbContext<EdgesDbContext>(dbContextFactory);
    services.AddScoped(postgresTableWriterFactory);
    services.AddScoped(databaseWriterFactory);
    
    Console.WriteLine("Database services registration completed.");
}

static (Action<DbContextOptionsBuilder>, Func<IServiceProvider, IPostgresTableWriter>, Func<IServiceProvider, IDatabaseWriter>) CreateDatabaseFactories(IConfiguration config)
{
    Action<DbContextOptionsBuilder> dbContextFactory;
    Func<IServiceProvider, IPostgresTableWriter> postgresTableWriterFactory;
    Func<IServiceProvider, IDatabaseWriter> databaseWriterFactory;

    try
    {
        var connectionString = config.GetConnectionString("PostgreSQLConnection");
        if (!string.IsNullOrEmpty(connectionString))
        {
            // Register the actual implementations
            dbContextFactory = options => { options.UseNpgsql(connectionString); };
            postgresTableWriterFactory = _ => new PostgresTableWriter();
            databaseWriterFactory = provider => new ExcelDatasetWriter(
                provider.GetRequiredService<IPostgresTableWriter>(),
                provider.GetRequiredService<ILogger<ExcelDatasetWriter>>());

            Console.WriteLine("✓ Database services registered with PostgreSQL");
        }
        else
        {
            // Register stub implementations when a database is not available
            (dbContextFactory, postgresTableWriterFactory, databaseWriterFactory) =
                CreateStubImplementations("Database not configured - PostgreSQL connection string missing");

            Console.WriteLine("⚠ Database services registered as stubs (no PostgreSQL connection string)");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠ Database registration failed: {ex.Message}");

        // Register stub implementations on error
        (dbContextFactory, postgresTableWriterFactory, databaseWriterFactory) =
            CreateStubImplementations($"Database error: {ex.Message}");
    }

    return (dbContextFactory, postgresTableWriterFactory, databaseWriterFactory);
}

static (Action<DbContextOptionsBuilder>, Func<IServiceProvider, IPostgresTableWriter>, Func<IServiceProvider, IDatabaseWriter>) CreateStubImplementations(string errorMessage)
{
    Console.WriteLine($"Creating stub implementations: {errorMessage}");
    
    return (
        _ => throw new InvalidOperationException(errorMessage),
        _ => throw new InvalidOperationException(errorMessage),
        _ => throw new InvalidOperationException(errorMessage)
    );
}

static void RegisterGraph(IServiceCollection svcs)
{
    Console.WriteLine("Starting Graph services registration...");
    
    svcs.AddScoped<GraphServiceClient>(_ => new GraphServiceClient(new DefaultAzureCredential()));
    svcs.AddScoped<IGraphFacade, GraphFacade>();
    
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
    
    svcs.AddScoped<IExcelParser, ExcelDataParser>();
    svcs.AddScoped<IDatabaseWriter, ExcelDatasetWriter>();
    
    // Add WebhookRegistrationService service
    svcs.AddScoped<WebhookRegistration>();

    svcs.AddScoped<SharePointChangeHandler>(p => new SharePointChangeHandler(
        p.GetRequiredService<ILogger<SharePointChangeHandler>>(),
        p.GetRequiredService<IGraphFacade>(),
        p.GetRequiredService<IExcelParser>(),
        p.GetRequiredService<IDatabaseWriter>(),
        p.GetRequiredService<EdgesDbContext>(),
        cfg["SharePoint:SiteId"] ?? "MISSING",
        cfg["SharePoint:ListId"] ?? "MISSING"));
    
    var siteId = cfg["SharePoint:SiteId"] ?? "MISSING";
    var listId = cfg["SharePoint:ListId"] ?? "MISSING";
    Console.WriteLine($"✓ Business services registration completed. SharePoint SiteId: {siteId}, ListId: {listId}");
}