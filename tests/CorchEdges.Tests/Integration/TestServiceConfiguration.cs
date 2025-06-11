using Azure.Identity;
using DotNetEnv;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;

namespace CorchEdges.Tests.Integration;

public static class TestServiceConfiguration
{
    public static IConfiguration CreateTestConfiguration()
    {
        Env.Load();

        return new ConfigurationBuilder()
            .AddJsonFile("appsettings.integration.json", optional: true)
            .AddJsonFile("appsettings.development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
    }

    public static void AddBaseServices(IServiceCollection services)
    {
        services.AddSingleton<IConfiguration>(CreateTestConfiguration());
        services.AddLogging(builder => builder.AddConsole());
        services.AddScoped<GraphServiceClient>(_ => new GraphServiceClient(new DefaultAzureCredential()));
    }
}