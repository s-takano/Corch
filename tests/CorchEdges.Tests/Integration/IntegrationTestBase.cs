using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace CorchEdges.Tests.Integration;

public abstract class IntegrationTestBase : IClassFixture<IntegrationTestFixture>, IAsyncLifetime
{
    protected IServiceProvider Services => Fixture.Services;
    protected IntegrationTestFixture Fixture { get; }
    protected readonly ITestOutputHelper Output;
    protected readonly IConfiguration Configuration;

    protected IntegrationTestBase(IntegrationTestFixture fixture, ITestOutputHelper output)
    {
        Fixture = fixture;
        Output = output;
        
        // Apply the specific test configuration
        fixture.ConfigureServices(ConfigureServices, ConfigureBuilder);
        
        Configuration = Services.GetRequiredService<IConfiguration>();
    }

    protected virtual void ConfigureBuilder(IConfigurationBuilder builder)
    {
        
    }

    /// <summary>
    /// Override this method in concrete test classes to configure services specific to that test class
    /// </summary>
    protected virtual void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<TokenCredential, DefaultAzureCredential>();
    }

    public virtual async Task InitializeAsync()
    {
        await Task.CompletedTask;
    }

    public virtual async Task DisposeAsync()
    {
        await Fixture.DisposeAsync();
    }
}