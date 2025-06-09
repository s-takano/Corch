using Azure.Identity;
using CorchEdges;
using CorchEdges.Abstractions;
using CorchEdges.Data;
using CorchEdges.Data.Abstractions;
using CorchEdges.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
// ... your other using statements

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        // Register GraphServiceClient with DefaultAzureCredential
        services.AddScoped<GraphServiceClient>(provider =>
        {
            var credential = new DefaultAzureCredential();
            return new GraphServiceClient(credential);
        });

        // Register your GraphFacade
        services.AddScoped<IGraphFacade, GraphFacade>();

        // Register other dependencies
        services.AddScoped<SharePointChangeHandler>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<SharePointChangeHandler>>();
            var graph = provider.GetRequiredService<IGraphFacade>();
            var parser = provider.GetRequiredService<IExcelParser>(); // You'll need to register this
            var dbWriter = provider.GetRequiredService<IDatabaseWriter>(); // You'll need to register this
            var context = provider.GetRequiredService<EdgesDbContext>(); // You'll need to register this
            
            // Get site and list IDs from configuration
            var config = provider.GetRequiredService<IConfiguration>();
            var siteId = config["SharePoint:SiteId"]!;
            var listId = config["SharePoint:ListId"]!;
            
            return new SharePointChangeHandler(logger, graph, parser, dbWriter, context, siteId, listId);
        });

        // Add other services as needed
        // services.AddScoped<IExcelParser, YourExcelParserImplementation>();
        // services.AddScoped<IDatabaseWriter, YourDatabaseWriterImplementation>();
        // services.AddDbContext<EdgesDbContext>(...);
    })
    .Build();

host.Run();