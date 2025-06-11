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
        fixture.ConfigureServices(ConfigureServices);
        
        Configuration = Services.GetRequiredService<IConfiguration>();
    }

    /// <summary>
    /// Override this method in concrete test classes to configure services specific to that test class
    /// </summary>
    protected virtual void ConfigureServices(IServiceCollection services)
    {
        // Default implementation - can be empty or provide common services
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await Fixture.DisposeAsync();
    }
}