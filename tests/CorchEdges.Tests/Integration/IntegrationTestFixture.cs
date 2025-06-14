using Azure.Identity;
using CorchEdges.Abstractions;
using CorchEdges.Tests.Helpers;
using CorchEdges.Utilities;
using DotNetEnv;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Graph;

namespace CorchEdges.Tests.Integration;

public class IntegrationTestFixture : IAsyncLifetime
{
    public ServiceProvider Services { get; private set; } = null!;

    internal void ConfigureServices(
        Action<IServiceCollection>? configureServices = null, 
        Action<IConfigurationBuilder>? customConfigureBuilder = null)
    {
        ServiceCollection serviceCollection = [];

        TestConfiguration.AddBaseServices(serviceCollection, customConfigureBuilder);

        // Register GraphServiceClient with DefaultAzureCredential
        serviceCollection.AddScoped<GraphServiceClient>(provider =>
        {
            var credential = new DefaultAzureCredential();
            return new GraphServiceClient(credential);
        });

        configureServices?.Invoke(serviceCollection);

        Services = serviceCollection.BuildServiceProvider();
    }


    public string GetTestSiteId()
    {
        var configuration = Services.GetRequiredService<IConfiguration>();
        return configuration["SharePoint:TestSiteId"] ??
               Environment.GetEnvironmentVariable("TEST_SHAREPOINT_SITE_ID") ??
               throw new InvalidOperationException("Test SharePoint Site ID not configured");
    }

    public string GetTestListId()
    {
        var configuration = Services.GetRequiredService<IConfiguration>();
        return configuration["SharePoint:TestListId"] ??
               Environment.GetEnvironmentVariable("TEST_SHAREPOINT_LIST_ID") ??
               throw new InvalidOperationException("Test SharePoint List ID not configured");
    }


    public void Dispose()
    {
        if (Services is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await Services.DisposeAsync();
    }
}