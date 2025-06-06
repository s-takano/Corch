using Microsoft.Extensions.Hosting;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using CorchEdges.Data;
using Azure.Storage.Blobs;
using CorchEdges;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        // Database
        services.AddDbContext<EdgesDbContext>(options =>
            options.UseNpgsql(context.Configuration.GetConnectionString("DefaultConnection")));

        // Core services
        services.AddScoped<IDatabaseWriter, ExcelDatasetWriter>();
        services.AddScoped<IPostgresTableWriter, PostgresTableWriter>(); 
        services.AddScoped<IExcelParser, ExcelDataParser>();
        services.AddScoped<IWebhookProcessor, DefaultWebhookProcessor>();
        
        // Graph and Storage services
        services.AddScoped<IGraphFacade, GraphFacade>(); // You need to implement this
        services.AddSingleton(sp => new BlobServiceClient(context.Configuration.GetConnectionString("AzureWebJobsStorage")));
        
        // ChangeHandler with configuration
        services.AddScoped<ChangeHandler>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ChangeHandler>>();
            var graph = sp.GetRequiredService<IGraphFacade>();
            var parser = sp.GetRequiredService<IExcelParser>();
            var db = sp.GetRequiredService<IDatabaseWriter>();
            var dbContext = sp.GetRequiredService<EdgesDbContext>(); // Renamed for clarity
            
            // Get configuration from the service provider
            var configuration = sp.GetRequiredService<IConfiguration>();
            var siteId = configuration["SharePoint:SiteId"]!;
            var listId = configuration["SharePoint:ListId"]!;
            
            return new ChangeHandler(logger, graph, parser, db, dbContext, siteId, listId);
        });
    });

var host = builder.Build();
host.Run();