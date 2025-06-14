using DotNetEnv;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CorchEdges.Tests.Helpers;

public static class TestConfiguration
{
    private static IConfiguration CreateTestConfiguration(Action<IConfigurationBuilder>? customConfigureBuilder)
    {
        Env.Load();

        var configurationBuilder = new ConfigurationBuilder()
            .AddJsonFile("appsettings.integration.json", optional: true)
            .AddJsonFile("appsettings.development.json", optional: true)
            .AddEnvironmentVariables();
        
        customConfigureBuilder?.Invoke(configurationBuilder);
        
        return configurationBuilder.Build();
    }

    public static void AddBaseServices(IServiceCollection services, Action<IConfigurationBuilder>? customConfigureBuilder = null)
    {
        services.AddSingleton(CreateTestConfiguration(customConfigureBuilder));
        services.AddLogging(builder => builder.AddConsole());
    }
}