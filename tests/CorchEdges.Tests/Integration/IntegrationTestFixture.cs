using Azure.Identity;
using CorchEdges.Abstractions;
using CorchEdges.Utilities;
using DotNetEnv;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Graph;

namespace CorchEdges.Tests.Integration;

public class IntegrationTestFixture : IAsyncLifetime
{
    public ServiceProvider Services { get; private set; }

    internal void ConfigureServices(Action<IServiceCollection> configureServices = null)
    {
        // Load .env file - automatically finds it in current directory or parent directories
        Env.Load();

        ServiceCollection serviceCollection = [];

        TestServiceConfiguration.AddBaseServices(serviceCollection);

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

    [Fact]
    public void CanReadTestConfiguration()
    {
        var config = Services.GetRequiredService<IConfiguration>();

        // From appsettings.test.json
        var logLevel = config["Logging:LogLevel:Default"];
        var timeout = config.GetValue<int>("SharePoint:DefaultTimeoutMs");

        // From .env (environment variables) 
        var siteId = config["TEST_SHAREPOINT_SITE_ID"];

        Assert.Equal("Information", logLevel);
        Assert.Equal(30000, timeout);
        Assert.NotNull(siteId);
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