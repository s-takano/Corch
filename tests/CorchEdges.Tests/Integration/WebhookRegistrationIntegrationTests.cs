using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Models;
using CorchEdges.Services;
using FluentAssertions;
using Microsoft.Graph;
using Xunit.Abstractions;

namespace CorchEdges.Tests.Integration;

[Collection("Integration")]
public class WebhookRegistrationIntegrationTests : IntegrationTestBase
{
    private WebhookRegistration _webhookRegistration;
    private readonly ILogger<WebhookRegistrationIntegrationTests> _logger;
    private readonly List<string> _createdSubscriptionIds = new();

    public WebhookRegistrationIntegrationTests(IntegrationTestFixture fixture, ITestOutputHelper output) 
        : base(fixture, output)
    {
        _webhookRegistration = Services.GetRequiredService<WebhookRegistration>();
        _logger = Services.GetRequiredService<ILogger<WebhookRegistrationIntegrationTests>>();
    }

    protected override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<GraphServiceClient>(_ => new GraphServiceClient(new DefaultAzureCredential()));
        services.AddScoped<WebhookRegistration>();
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
        var subscription = await _webhookRegistration.RegisterWebhookAsync(
            siteId,
            listId,
            callbackUrl,
            clientState: $"test-{testId}");

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
        var firstSubscription = await _webhookRegistration.RegisterWebhookAsync(
            siteId, 
            listId, 
            callbackUrl,
            clientState: $"test-{testId}");
        _createdSubscriptionIds.Add(firstSubscription.Id!);

        // Act - Register again with same URL
        var secondSubscription = await _webhookRegistration.RegisterWebhookAsync(
            siteId, 
            listId, 
            callbackUrl,
            clientState: $"test-{testId}");

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
            await _webhookRegistration.RegisterWebhookAsync(siteId, listId, invalidCallbackUrl));
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
        var subscription = await _webhookRegistration.RegisterWebhookAsync(
            siteId, 
            listId, 
            callbackUrl,
            clientState: $"test-{testId}");

        _createdSubscriptionIds.Add(subscription.Id!);

        // Act
        var isMonitored = await _webhookRegistration.IsListMonitoredByWebhookAsync(siteId, listId);

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
        var isMonitored = await _webhookRegistration.IsListMonitoredByWebhookAsync(siteId, nonExistentListId);

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
        var subscription = await _webhookRegistration.RegisterWebhookAsync(
            siteId, 
            listId, 
            callbackUrl,
            clientState: $"test-{testId}");

        _createdSubscriptionIds.Add(subscription.Id!);

        // Act
        var isRegistered = await _webhookRegistration.IsSpecificWebhookRegisteredAsync(siteId, listId, callbackUrl);

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
        var status = await _webhookRegistration.GetListWebhookStatusAsync(emptySiteId, emptyListId);

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
        var subscription = await _webhookRegistration.RegisterWebhookAsync(
            siteId, 
            listId, 
            callbackUrl,
            clientState: $"test-{testId}");

        _createdSubscriptionIds.Add(subscription.Id!);

        // Get status
        var status = await _webhookRegistration.GetListWebhookStatusAsync(siteId, listId);

        // Assert
        status.HasWebhooks.Should().BeTrue();
        status.Count.Should().BeGreaterThanOrEqualTo(1);
        status.IsRegisteredTo(callbackUrl).Should().BeTrue();
        status.IsRegisteredTo("https://different-url.com").Should().BeFalse();
        
        // Check the specific subscription details
        var subscriptionInfo = status.GetByCallbackUrl(callbackUrl);
        subscriptionInfo.Should().NotBeNull();
        subscriptionInfo!.SubscriptionId.Should().Be(subscription.Id);
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

        var subscription = await _webhookRegistration.RegisterWebhookAsync(
            siteId, 
            listId, 
            callbackUrl,
            clientState: $"test-{testId}");
        
        _createdSubscriptionIds.Add(subscription.Id!);

        // Check monitoring status
        var isMonitored = await _webhookRegistration.IsListMonitoredByWebhookAsync(siteId, listId);

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
        var subscription = await _webhookRegistration.RegisterWebhookAsync(
            siteId, 
            listId, 
            callbackUrl,
            clientState: $"test-{testId}");

        _createdSubscriptionIds.Add(subscription.Id!);

        // Check specific registration
        var isRegistered = await _webhookRegistration.IsSpecificWebhookRegisteredAsync(siteId, listId, callbackUrl);
        var isNotRegistered = await _webhookRegistration.IsSpecificWebhookRegisteredAsync(siteId, listId, "https://different.com/webhook");

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

        var subscription = await _webhookRegistration.RegisterWebhookAsync(
            siteId, 
            listId, 
            callbackUrl,
            clientState: $"test-{testId}");
        
        _createdSubscriptionIds.Add(subscription.Id!);

        // Act
        var activeSubscriptions = await _webhookRegistration.GetActiveSubscriptionsAsync();

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

        var subscription = await _webhookRegistration.RegisterWebhookAsync(
            siteId, 
            listId, 
            callbackUrl,
            clientState: $"test-{testId}");
        
        _createdSubscriptionIds.Add(subscription.Id!);

        // Act
        var renewResult = await _webhookRegistration.RenewSubscriptionAsync(subscription.Id!, 2);

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

        var subscription = await _webhookRegistration.RegisterWebhookAsync(
            siteId, 
            listId, 
            callbackUrl,
            clientState: $"test-{testId}");

        _createdSubscriptionIds.Add(subscription.Id!);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await _webhookRegistration.RenewSubscriptionAsync(subscription.Id!, 5)); // More than 3 days
    }

    [Fact]
    public async Task RenewSubscriptionAsync_WithInvalidSubscriptionId_ShouldReturnFalse()
    {
        // Arrange
        var invalidSubscriptionId = Guid.NewGuid().ToString();

        // Act
        var renewResult = await _webhookRegistration.RenewSubscriptionAsync(invalidSubscriptionId);

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

        var subscription = await _webhookRegistration.RegisterWebhookAsync(
            siteId, 
            listId, 
            callbackUrl,
            clientState: $"test-{testId}");

        // Act
        var deleteResult = await _webhookRegistration.DeleteSubscriptionAsync(subscription.Id!);

        // Assert
        deleteResult.Should().BeTrue();
        
        // Verify it's actually deleted by checking if the specific webhook is still registered
        var isStillRegistered = await _webhookRegistration.IsSpecificWebhookRegisteredAsync(siteId, listId, callbackUrl);
        isStillRegistered.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteSubscriptionAsync_WithInvalidSubscriptionId_ShouldReturnFalse()
    {
        // Arrange
        var invalidSubscriptionId = Guid.NewGuid().ToString();

        // Act
        var deleteResult = await _webhookRegistration.DeleteSubscriptionAsync(invalidSubscriptionId);

        // Assert
        deleteResult.Should().BeFalse();
    }

    [Fact]
    public async Task GetExpiringSubscriptionsAsync_ShouldReturnExpiringSubscriptions()
    {
        // Arrange
        var timeWindow = TimeSpan.FromDays(30); // Look for subscriptions expiring in 30 days

        // Act
        var expiringSubscriptions = await _webhookRegistration.GetExpiringSubscriptionsAsync(timeWindow);

        // Assert
        var subscriptions = expiringSubscriptions as Subscription[] ?? expiringSubscriptions.ToArray();
     
        subscriptions.Should().NotBeNull();
        foreach (var subscription in subscriptions)
        {
            subscription.ExpirationDateTime.Should().BeBefore(DateTimeOffset.UtcNow.Add(timeWindow));
        }
    }

    public Task InitializeAsync()
    {
        _logger.LogInformation("Starting WebhookRegistration integration tests");
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        // Cleanup any subscriptions that weren't explicitly deleted in tests
        foreach (var subscriptionId in _createdSubscriptionIds)
        {
            try
            {
                await _webhookRegistration.DeleteSubscriptionAsync(subscriptionId);
                _logger.LogInformation("Cleaned up subscription {SubscriptionId}", subscriptionId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup subscription {SubscriptionId}", subscriptionId);
            }
        }
    }
}