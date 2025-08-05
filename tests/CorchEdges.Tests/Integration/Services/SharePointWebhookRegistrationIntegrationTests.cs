using Azure.Identity;
using CorchEdges.Services;
using CorchEdges.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace CorchEdges.Tests.Integration.Services;

[Trait("Category", TestCategories.Integration)]
[Trait("Requires", InfrastructureRequirements.AzureGraphApi)]
[Collection("Integration")]
public class SharePointWebhookRegistrationIntegrationTests : IntegrationTestBase
{
    private SharePointWebhookRegistrar _sharePointWebhookRegistrar;
    private readonly ILogger<SharePointWebhookRegistrationIntegrationTests> _logger;
    private readonly List<string> _createdSubscriptionIds = new();

    public SharePointWebhookRegistrationIntegrationTests(IntegrationTestFixture fixture, ITestOutputHelper output) 
        : base(fixture, output)
    {
        _sharePointWebhookRegistrar = Services.GetRequiredService<SharePointWebhookRegistrar>();
        _logger = Services.GetRequiredService<ILogger<SharePointWebhookRegistrationIntegrationTests>>();
    }

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);
        services.AddScoped<GraphServiceClient>(_ => new GraphServiceClient(new DefaultAzureCredential()));
        services.AddScoped<SharePointWebhookRegistrar>();
    }

    [Fact]
    public async Task RegisterWebhookAsync_WithValidParameters_ShouldCreateSubscription()
    {
        // Arrange
        var siteId = Fixture.GetTestSiteId();
        var listId = Fixture.GetTestListId(); 

        var testId = Guid.NewGuid().ToString("N")[..8];
        var functionKey = Environment.GetEnvironmentVariable("TEST_FUNCTION_KEY") ?? throw new Exception("TEST_FUNCTION_KEY environment variable not set");
        var callbackUrl = $"https://corch-edges.azurewebsites.net/test/webhook?code={functionKey}";
        
        
        // Act
        var subscription = await _sharePointWebhookRegistrar.RegisterWebhookAsync(
            siteId,
            listId,
            callbackUrl,
            clientState: $"test-{testId}",
            TestContext.Current.CancellationToken);

        // Assert
        subscription.Should().NotBeNull();
        subscription.Id.Should().NotBeNullOrEmpty();
        subscription.NotificationUrl.Should().Be(callbackUrl);
        subscription.Resource.Should().Contain(listId);
        subscription.ExpirationDateTime.Should().BeAfter(DateTimeOffset.UtcNow);
        
        // Track for cleanup
        if (!string.IsNullOrEmpty(subscription.Id))
            _createdSubscriptionIds.Add(subscription.Id);
    }

    [Fact]
    public async Task RegisterWebhookAsync_WithSameCallbackUrl_ShouldReturnExistingSubscription()
    {
        // Arrange
        var siteId = Fixture.GetTestSiteId();
        var listId = Fixture.GetTestListId();
        var testId = Guid.NewGuid().ToString("N")[..8];
        var functionKey = Environment.GetEnvironmentVariable("TEST_FUNCTION_KEY") ?? throw new Exception("TEST_FUNCTION_KEY environment variable not set");
        var callbackUrl = $"https://corch-edges.azurewebsites.net/test/webhook?code={functionKey}";

        // Act - Register webhook first
        var firstSubscription = await _sharePointWebhookRegistrar.RegisterWebhookAsync(
            siteId, 
            listId, 
            callbackUrl,
            clientState: $"test-{testId}",
            TestContext.Current.CancellationToken);
        _createdSubscriptionIds.Add(firstSubscription.Id!);

        // Act - Register again with same URL
        var secondSubscription = await _sharePointWebhookRegistrar.RegisterWebhookAsync(
            siteId, 
            listId, 
            callbackUrl,
            clientState: $"test-{testId}",
            TestContext.Current.CancellationToken);

        // Assert
        secondSubscription.Should().NotBeNull();
        secondSubscription.Id.Should().Be(firstSubscription.Id);
        secondSubscription.NotificationUrl.Should().Be(callbackUrl);
    }

    [Fact]
    public async Task RegisterWebhookAsync_WithInvalidCallbackUrl_ShouldThrowException()
    {
        // Arrange
        var siteId = Fixture.GetTestSiteId();
        var listId = Fixture.GetTestListId();
        var invalidCallbackUrl = "not-a-valid-url";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _sharePointWebhookRegistrar.RegisterWebhookAsync(siteId, listId, invalidCallbackUrl, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task IsListMonitoredByWebhookAsync_WithExistingWebhook_ShouldReturnTrue_RegardlessOfCallbackUrl()
    {
        // Arrange
        var siteId = Fixture.GetTestSiteId();
        var listId = Fixture.GetTestListId();
        var testId = Guid.NewGuid().ToString("N")[..8];

        var functionKey = Environment.GetEnvironmentVariable("TEST_FUNCTION_KEY") ?? throw new Exception("TEST_FUNCTION_KEY environment variable not set");
        var callbackUrl = $"https://corch-edges.azurewebsites.net/test/webhook?code={functionKey}";

        // Act - Register webhook first
        var subscription = await _sharePointWebhookRegistrar.RegisterWebhookAsync(
            siteId, 
            listId, 
            callbackUrl,
            clientState: $"test-{testId}",
            TestContext.Current.CancellationToken);

        _createdSubscriptionIds.Add(subscription.Id!);

        // Act
        var isMonitored = await _sharePointWebhookRegistrar.IsListMonitoredByWebhookAsync(siteId, listId, 30, TestContext.Current.CancellationToken);

        // Assert
        isMonitored.Should().BeTrue();
    }

    [Fact]
    public async Task IsListMonitoredByWebhookAsync_WithNonExistentWebhook_ShouldReturnFalse()
    {
        // Arrange
        var siteId =Fixture.GetTestSiteId();
        var nonExistentListId = Guid.NewGuid().ToString();

        // Act
        var isMonitored = await _sharePointWebhookRegistrar.IsListMonitoredByWebhookAsync(siteId, nonExistentListId, 30, TestContext.Current.CancellationToken);

        // Assert
        isMonitored.Should().BeFalse();
    }

    [Fact]
    public async Task IsSpecificWebhookRegisteredAsync_WithMatchingCallbackUrl_ShouldReturnTrue()
    {
        // Arrange
        var siteId = Fixture.GetTestSiteId();
        var listId = Fixture.GetTestListId();
        var testId = Guid.NewGuid().ToString("N")[..8];

        var functionKey = Environment.GetEnvironmentVariable("TEST_FUNCTION_KEY") ?? throw new Exception("TEST_FUNCTION_KEY environment variable not set");
        var callbackUrl = $"https://corch-edges.azurewebsites.net/test/webhook?code={functionKey}";

        // Act - Register webhook first
        var subscription = await _sharePointWebhookRegistrar.RegisterWebhookAsync(
            siteId, 
            listId, 
            callbackUrl,
            clientState: $"test-{testId}",
            TestContext.Current.CancellationToken);

        _createdSubscriptionIds.Add(subscription.Id!);

        // Act
        var isRegistered = await _sharePointWebhookRegistrar.IsSpecificWebhookRegisteredAsync(siteId, listId, callbackUrl, TestContext.Current.CancellationToken);

        // Assert
        isRegistered.Should().BeTrue();
    }

    [Fact]
    public async Task GetListWebhookStatusAsync_WithNoWebhooks_ShouldReturnEmptyStatus()
    {
        // Arrange
        var emptySiteId = "test-empty-site";
        var emptyListId = "test-empty-list";

        // Act
        var status = await _sharePointWebhookRegistrar.GetListWebhookStatusAsync(emptySiteId, emptyListId, 30, TestContext.Current.CancellationToken);

        // Assert
        status.HasWebhooks.Should().BeFalse();
        status.Count.Should().Be(0);
        status.Subscriptions.Should().BeEmpty();
    }

    [Fact]
    public async Task GetListWebhookStatusAsync_WithExistingWebhook_ShouldReturnCompleteStatus()
    {
        // Arrange
        var siteId = Fixture.GetTestSiteId();
        var listId = Fixture.GetTestListId();
        var testId = Guid.NewGuid().ToString("N")[..8];

        var functionKey = Environment.GetEnvironmentVariable("TEST_FUNCTION_KEY") ?? throw new Exception("TEST_FUNCTION_KEY environment variable not set");
        var callbackUrl = $"https://corch-edges.azurewebsites.net/test/webhook?code={functionKey}";

        // Act - Register webhook first
        var subscription = await _sharePointWebhookRegistrar.RegisterWebhookAsync(
            siteId, 
            listId, 
            callbackUrl,
            clientState: $"test-{testId}",
            TestContext.Current.CancellationToken);

        _createdSubscriptionIds.Add(subscription.Id!);

        // Get status
        var status = await _sharePointWebhookRegistrar.GetListWebhookStatusAsync(siteId, listId, 30, TestContext.Current.CancellationToken);

        // Assert
        status.HasWebhooks.Should().BeTrue();
        status.Count.Should().BeGreaterThanOrEqualTo(1);
        status.IsRegisteredTo(callbackUrl).Should().BeTrue();
        status.IsRegisteredTo("https://different-url.com").Should().BeFalse();
        
        // Check the specific subscription details
        var subscriptionInfo = status.GetByCallbackUrl(callbackUrl);
        subscriptionInfo.Should().NotBeNull();
        subscriptionInfo.SubscriptionId.Should().Be(subscription.Id);
        subscriptionInfo.CallbackUrl.Should().Be(callbackUrl);
        subscriptionInfo.ExpirationDate.Should().NotBeNull();
    }


    [Fact]
    public async Task IsListMonitoredByWebhookAsync_WithExistingWebhook_ShouldReturnTrue()
    {
        // Arrange
        var siteId = Fixture.GetTestSiteId();
        var listId = Fixture.GetTestListId();
        var testId = Guid.NewGuid().ToString("N")[..8];

        var functionKey = Environment.GetEnvironmentVariable("TEST_FUNCTION_KEY") ?? throw new Exception("TEST_FUNCTION_KEY environment variable not set");
        var callbackUrl = $"https://corch-edges.azurewebsites.net/test/webhook?code={functionKey}";

        var subscription = await _sharePointWebhookRegistrar.RegisterWebhookAsync(
            siteId, 
            listId, 
            callbackUrl,
            clientState: $"test-{testId}",
            TestContext.Current.CancellationToken);
        
        _createdSubscriptionIds.Add(subscription.Id!);

        // Check monitoring status
        var isMonitored = await _sharePointWebhookRegistrar.IsListMonitoredByWebhookAsync(siteId, listId, 30, TestContext.Current.CancellationToken);

        // Assert
        isMonitored.Should().BeTrue();
    }

    [Fact]
    public async Task IsSpecificWebhookRegisteredAsync_WithMatchingUrl_ShouldReturnTrue()
    {
        // Arrange
        var siteId = Fixture.GetTestSiteId();
        var listId = Fixture.GetTestListId();
        var testId = Guid.NewGuid().ToString("N")[..8];
        var functionKey = Environment.GetEnvironmentVariable("TEST_FUNCTION_KEY") ?? throw new Exception("TEST_FUNCTION_KEY environment variable not set");
        var callbackUrl = $"https://corch-edges.azurewebsites.net/test/webhook?code={functionKey}";

        // Act - Register webhook first
        var subscription = await _sharePointWebhookRegistrar.RegisterWebhookAsync(
            siteId, 
            listId, 
            callbackUrl,
            clientState: $"test-{testId}",
            TestContext.Current.CancellationToken);

        _createdSubscriptionIds.Add(subscription.Id!);

        // Check specific registration
        var isRegistered = await _sharePointWebhookRegistrar.IsSpecificWebhookRegisteredAsync(siteId, listId, callbackUrl, TestContext.Current.CancellationToken);
        var isNotRegistered = await _sharePointWebhookRegistrar.IsSpecificWebhookRegisteredAsync(siteId, listId, "https://different.com/webhook", TestContext.Current.CancellationToken);

        // Assert
        isRegistered.Should().BeTrue();
        isNotRegistered.Should().BeFalse();
    }

    [Fact]
    public async Task GetActiveSubscriptionsAsync_ShouldReturnSubscriptions()
    {
        // Arrange
        var siteId = Fixture.GetTestSiteId();
        var listId = Fixture.GetTestListId();
        var testId = Guid.NewGuid().ToString("N")[..8];

        var functionKey = Environment.GetEnvironmentVariable("TEST_FUNCTION_KEY") ?? throw new Exception("TEST_FUNCTION_KEY environment variable not set");
        var callbackUrl = $"https://corch-edges.azurewebsites.net/test/webhook?code={functionKey}";

        var subscription = await _sharePointWebhookRegistrar.RegisterWebhookAsync(
            siteId, 
            listId, 
            callbackUrl,
            clientState: $"test-{testId}",
            TestContext.Current.CancellationToken);
        
        _createdSubscriptionIds.Add(subscription.Id!);

        // Act
        var activeSubscriptions = await _sharePointWebhookRegistrar.GetActiveSubscriptionsAsync(30, TestContext.Current.CancellationToken);

        // Assert
        var subscriptions = activeSubscriptions as Subscription[] ?? activeSubscriptions.ToArray();
        
        subscriptions.Should().NotBeEmpty();
        subscriptions.Should().Contain(s => s.Id == subscription.Id);
    }

    [Fact]
    public async Task RenewSubscriptionAsync_WithValidSubscriptionId_ShouldReturnTrue()
    {
        // Arrange
        var siteId = Fixture.GetTestSiteId();
        var listId = Fixture.GetTestListId();
        var testId = Guid.NewGuid().ToString("N")[..8];

        var functionKey = Environment.GetEnvironmentVariable("TEST_FUNCTION_KEY") ?? throw new Exception("TEST_FUNCTION_KEY environment variable not set");
        var callbackUrl = $"https://corch-edges.azurewebsites.net/test/webhook?code={functionKey}";

        var subscription = await _sharePointWebhookRegistrar.RegisterWebhookAsync(
            siteId, 
            listId, 
            callbackUrl,
            clientState: $"test-{testId}", 
            TestContext.Current.CancellationToken);
        
        _createdSubscriptionIds.Add(subscription.Id!);

        // Act
        var renewResult = await _sharePointWebhookRegistrar.RenewSubscriptionAsync(subscription.Id!, 2, TestContext.Current.CancellationToken);

        // Assert
        renewResult.Should().BeTrue();
    }

    [Fact]
    public async Task RenewSubscriptionAsync_WithInvalidDays_ShouldThrowException()
    {
        // Arrange
        var siteId = Fixture.GetTestSiteId();
        var listId = Fixture.GetTestListId();
        var testId = Guid.NewGuid().ToString("N")[..8];
        var functionKey = Environment.GetEnvironmentVariable("TEST_FUNCTION_KEY") ?? throw new Exception("TEST_FUNCTION_KEY environment variable not set");
        var callbackUrl = $"https://corch-edges.azurewebsites.net/test/webhook?code={functionKey}";

        var subscription = await _sharePointWebhookRegistrar.RegisterWebhookAsync(
            siteId, 
            listId, 
            callbackUrl,
            clientState: $"test-{testId}",
            TestContext.Current.CancellationToken);

        _createdSubscriptionIds.Add(subscription.Id!);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await _sharePointWebhookRegistrar.RenewSubscriptionAsync(subscription.Id!, 5, TestContext.Current.CancellationToken)); // More than 3 days
    }

    [Fact]
    public async Task RenewSubscriptionAsync_WithInvalidSubscriptionId_ShouldReturnFalse()
    {
        // Arrange
        var invalidSubscriptionId = Guid.NewGuid().ToString();

        // Act
        var renewResult = await _sharePointWebhookRegistrar.RenewSubscriptionAsync(invalidSubscriptionId,3, TestContext.Current.CancellationToken);

        // Assert
        renewResult.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteSubscriptionAsync_WithValidSubscriptionId_ShouldReturnTrue()
    {
        // Arrange
        var siteId = Fixture.GetTestSiteId();
        var listId = Fixture.GetTestListId();
        var testId = Guid.NewGuid().ToString("N")[..8];
        
        var functionKey = Environment.GetEnvironmentVariable("TEST_FUNCTION_KEY") ?? throw new Exception("TEST_FUNCTION_KEY environment variable not set");
        var callbackUrl = $"https://corch-edges.azurewebsites.net/test/webhook?code={functionKey}";

        var subscription = await _sharePointWebhookRegistrar.RegisterWebhookAsync(
            siteId, 
            listId, 
            callbackUrl,
            clientState: $"test-{testId}",
            TestContext.Current.CancellationToken);

        // Act
        var deleteResult = await _sharePointWebhookRegistrar.DeleteSubscriptionAsync(subscription.Id!);

        // Assert
        deleteResult.Should().BeTrue();
        
        // Verify it's actually deleted by checking if the specific webhook is still registered
        var isStillRegistered = await _sharePointWebhookRegistrar.IsSpecificWebhookRegisteredAsync(siteId, listId, callbackUrl, TestContext.Current.CancellationToken);
        isStillRegistered.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteSubscriptionAsync_WithInvalidSubscriptionId_ShouldReturnFalse()
    {
        // Arrange
        var invalidSubscriptionId = Guid.NewGuid().ToString();

        // Act
        var deleteResult = await _sharePointWebhookRegistrar.DeleteSubscriptionAsync(invalidSubscriptionId);

        // Assert
        deleteResult.Should().BeFalse();
    }

    [Fact]
    public async Task GetExpiringSubscriptionsAsync_ShouldReturnExpiringSubscriptions()
    {
        // Arrange
        var timeWindow = TimeSpan.FromDays(30); // Look for subscriptions expiring in 30 days

        // Act
        var expiringSubscriptions = await _sharePointWebhookRegistrar.GetExpiringSubscriptionsAsync(timeWindow, TestContext.Current.CancellationToken);

        // Assert
        var subscriptions = expiringSubscriptions as Subscription[] ?? expiringSubscriptions.ToArray();
     
        subscriptions.Should().NotBeNull();
        foreach (var subscription in subscriptions)
        {
            subscription.ExpirationDateTime.Should().BeBefore(DateTimeOffset.UtcNow.Add(timeWindow));
        }
    }

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _logger.LogInformation("Starting SharePointWebhookRegistrar integration tests");
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        // Cleanup any subscriptions that weren't explicitly deleted in tests
        foreach (var subscriptionId in _createdSubscriptionIds)
        {
            try
            {
                await _sharePointWebhookRegistrar.DeleteSubscriptionAsync(subscriptionId);
                _logger.LogInformation("Cleaned up subscription {SubscriptionId}", subscriptionId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup subscription {SubscriptionId}", subscriptionId);
            }
        }
    }
}