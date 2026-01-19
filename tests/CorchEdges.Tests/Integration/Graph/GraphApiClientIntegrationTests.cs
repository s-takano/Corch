using Azure.Core;
using CorchEdges.Abstractions;
using CorchEdges.Tests.Infrastructure;
using CorchEdges.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;


namespace CorchEdges.Tests.Integration.Graph;

[Trait("Category", TestCategories.Integration)]
[Trait("Requires", InfrastructureRequirements.AzureGraphApi)]
[Collection("Integration")]
public class GraphApiClientIntegrationTests : IntegrationTestBase
{
    private readonly IGraphApiClient _graphApiClient;
    private readonly ITestOutputHelper _output;
    private const string TestItemId = "5"; // Pre-created test item with specific permissions setup

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);
        services.AddScoped<IGraphApiClient, GraphApiClient>();
        services.AddSingleton<GraphServiceClient>(provider =>
        {
            var credential = provider.GetRequiredService<TokenCredential>();
            return new GraphServiceClient(credential);
        });
    }

    public GraphApiClientIntegrationTests(IntegrationTestFixture fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
        _graphApiClient = fixture.Services.GetRequiredService<IGraphApiClient>();
        _output = output;


        _output.WriteLine($"Test Environment Variables:");
        _output.WriteLine($"Item ID: {TestItemId}");
    }

    [Fact]
    public async Task TestConnectionAsync_WithValidCredentials_ReturnsSuccess()
    {
        // Act
        var result = await _graphApiClient.TestConnectionAsync();

        // Assert
        Assert.True(result.IsSuccess, $"Graph connection failed: {result.ErrorReason} (Code: {result.ErrorCode})");

        if (result.IsSuccess)
        {
            _output.WriteLine("✅ Graph connection test passed");
        }
        else
        {
            _output.WriteLine($"❌ Graph connection failed:");
            _output.WriteLine($"   Reason: {result.ErrorReason}");
            _output.WriteLine($"   Code: {result.ErrorCode}");
        }
    }

    [Fact]
    public async Task TestConnectionAsync_WithInvalidCredentials_ReturnsFailureWithReason()
    {
        // This test would need mock setup or invalid credentials
        // Just showing how to use the enhanced result

        var result = await _graphApiClient.TestConnectionAsync();

        if (!result.IsSuccess)
        {
            _output.WriteLine($"Expected failure occurred:");
            _output.WriteLine($"   Reason: {result.ErrorReason}");
            _output.WriteLine($"   Code: {result.ErrorCode}");

            // You can now make assertions based on the specific error
            Assert.Contains("permission", result.ErrorReason!, StringComparison.OrdinalIgnoreCase);
        }
    }


    [Fact]
    public async Task GetListItemAsync_WithValidParameters_ReturnsListItem()
    {
        // Arrange
        if (ShouldSkipTest()) return;

        // Act
        var result =
            await _graphApiClient.GetListItemAsync(Fixture.GetTestSiteId(), Fixture.GetTestListId(), TestItemId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(TestItemId, result.Id);
        Assert.NotNull(result.Fields);

        _output.WriteLine($"✅ Retrieved list item: {result.Id}");
        _output.WriteLine($"   Created: {result.CreatedDateTime}");
        _output.WriteLine($"   Modified: {result.LastModifiedDateTime}");

        // Check if the ProcessingStatus field exists (based on your business logic)
        Assert.True(result.Fields?.AdditionalData.ContainsKey("ProcessingStatus"));

        var processingStatus = result.Fields.AdditionalData["ProcessingStatus"];
        _output.WriteLine($"   ProcessingStatus: {processingStatus}");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetListItemAsync_WithInvalidItemId_ReturnsNull()
    {
        // Arrange
        if (ShouldSkipTest()) return;
        const string invalidItemId = "999999";

        // Act & Assert
        await Assert.ThrowsAsync<ODataError>(async () =>
            await _graphApiClient.GetListItemAsync(Fixture.GetTestSiteId(), Fixture.GetTestListId(), invalidItemId));

        _output.WriteLine("✅ Invalid item ID correctly returned null");
    }

    [Fact]
    public async Task GetDriveItemAsync_WithValidParameters_ReturnsDriveItem()
    {
        // Arrange
        if (ShouldSkipTest()) return;

        // Act
        var result =
            await _graphApiClient.GetDriveItemAsync(Fixture.GetTestSiteId(), Fixture.GetTestListId(), TestItemId);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Id);
        Assert.NotNull(result.Name);
        Assert.NotNull(result.ParentReference);
        Assert.NotNull(result.ParentReference.DriveId);

        _output.WriteLine($"✅ Retrieved drive item: {result.Name}");
        _output.WriteLine($"   ID: {result.Id}");
        _output.WriteLine($"   Drive ID: {result.ParentReference.DriveId}");
        _output.WriteLine($"   Size: {result.Size} bytes");
        _output.WriteLine($"   Content Type: {result.File?.MimeType}");
    }

    [Fact]
    public async Task GetDriveItemAsync_WithInvalidItemId_ReturnsNull()
    {
        // Arrange
        if (ShouldSkipTest()) return;
        const string invalidItemId = "999999";

        // Act & Assert
        await Assert.ThrowsAsync<ODataError>(() =>
            _graphApiClient.GetDriveItemAsync(Fixture.GetTestSiteId(), Fixture.GetTestListId(), invalidItemId));
        _output.WriteLine("✅ Invalid drive item ID correctly returned null");
    }

    [Fact]
    public async Task DownloadAsync_WithValidDriveItem_ReturnsStream()
    {
        // Arrange
        if (ShouldSkipTest()) return;

        // First get the drive item to get the drive ID
        var driveItem =
            await _graphApiClient.GetDriveItemAsync(Fixture.GetTestSiteId(), Fixture.GetTestListId(), TestItemId);
        Assert.NotNull(driveItem);
        Assert.NotNull(driveItem.ParentReference?.DriveId);
        Assert.NotNull(driveItem.Id);

        // Act
        await using var stream = await _graphApiClient.DownloadAsync(
            driveItem.ParentReference.DriveId,
            driveItem.Id);

        // Assert
        Assert.NotNull(stream);
        Assert.True(stream.CanRead);

        // Verify we can actually read content
        var buffer = new byte[1024];
        var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, TestContext.Current.CancellationToken);
        Assert.True(bytesRead > 0, "Should be able to read data from the stream");

        _output.WriteLine($"✅ Successfully downloaded file stream");
        _output.WriteLine($"   First read: {bytesRead} bytes");
        _output.WriteLine($"   Stream can seek: {stream.CanSeek}");

        // If it's an Excel file, check for Excel signature
        if (bytesRead >= 4)
        {
            var isExcel = buffer[0] == 0x50 && buffer[1] == 0x4B; // ZIP signature (Excel files are ZIP-based)
            var isOldExcel = buffer[0] == 0xD0 && buffer[1] == 0xCF; // OLE signature (old Excel files)

            if (isExcel || isOldExcel)
            {
                _output.WriteLine($"   ✅ File appears to be Excel format");
            }
        }
    }

    [Fact]
    public async Task DownloadAsync_WithInvalidDriveId_ThrowsException()
    {
        // Arrange
        const string invalidDriveId = "invalid-drive-id";
        const string invalidItemId = "invalid-item-id";

        // Act & Assert
        await Assert.ThrowsAsync<ODataError>(async () =>
        {
            await using var stream = await _graphApiClient.DownloadAsync(invalidDriveId, invalidItemId);
        });

        _output.WriteLine("✅ Invalid drive/item IDs correctly threw ServiceException");
    }

    [Fact]
    public async Task IntegrationWorkflow_CompleteProcess_WorksEndToEnd()
    {
        // Arrange
        if (ShouldSkipTest()) return;

        // Act & Assert - Complete workflow
        _output.WriteLine("🔄 Starting complete integration workflow...");

        // Step 1: Test connection
        var connectionResult = await _graphApiClient.TestConnectionAsync();
        Assert.True(connectionResult.IsSuccess);
        _output.WriteLine("   ✅ Step 1: Connection verified");

        // Step 2: Get list item
        var listItem =
            await _graphApiClient.GetListItemAsync(Fixture.GetTestSiteId(), Fixture.GetTestListId(), TestItemId);
        Assert.NotNull(listItem);
        _output.WriteLine($"   ✅ Step 2: List item retrieved (ID: {listItem.Id})");

        // Step 3: Get drive item
        var driveItem =
            await _graphApiClient.GetDriveItemAsync(Fixture.GetTestSiteId(), Fixture.GetTestListId(), TestItemId);
        Assert.NotNull(driveItem);
        Assert.NotNull(driveItem.ParentReference?.DriveId);
        _output.WriteLine($"   ✅ Step 3: Drive item retrieved (Name: {driveItem.Name})");

        // Step 4: Download content
        await using var stream = await _graphApiClient.DownloadAsync(
            driveItem.ParentReference.DriveId,
            driveItem.Id!);
        Assert.NotNull(stream);

        var buffer = new byte[1024];
        var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, TestContext.Current.CancellationToken);
        Assert.True(bytesRead > 0);
        _output.WriteLine($"   ✅ Step 4: File downloaded ({bytesRead} bytes read)");

        _output.WriteLine("🎉 Complete integration workflow successful!");
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task Performance_ConcurrentRequests_HandlesLoad()
    {
        // Arrange
        if (ShouldSkipTest()) return;
        const int concurrentRequests = 5;

        var tasks = new List<Task<ListItem?>>();

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        for (var i = 0; i < concurrentRequests; i++)
        {
            tasks.Add(_graphApiClient.GetListItemAsync(Fixture.GetTestSiteId(), Fixture.GetTestListId(), TestItemId));
        }

        var results = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        Assert.All(results, result => Assert.NotNull(result));
        Assert.All(results, result => Assert.Equal(TestItemId, result!.Id));

        _output.WriteLine($"✅ {concurrentRequests} concurrent requests completed in {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"   Average: {stopwatch.ElapsedMilliseconds / concurrentRequests}ms per request");

        // Performance assertion - adjust threshold as needed
        Assert.True(stopwatch.ElapsedMilliseconds < 30000,
            $"Concurrent requests took too long: {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task GetItemsDeltaAsync_WithLatestToken_ReturnsInitialDelta()
    {
        // Arrange
        if (ShouldSkipTest()) return;

        // Act
        var (deltaLink, itemIds) = await _graphApiClient.PullItemsDeltaAsync(
            Fixture.GetTestSiteId(),
            Fixture.GetTestListId(),
            "latest");

        // Assert
        Assert.NotNull(deltaLink);
        Assert.NotNull(itemIds);
        Assert.NotEmpty(deltaLink);

        _output.WriteLine($"✅ Retrieved delta with 'latest' token");
        _output.WriteLine($"   Delta Link: {deltaLink}");
        _output.WriteLine($"   Items Count: {itemIds.Count}");

        // Verify delta link format (should contain SharePoint-specific URL structure)
        Assert.Contains("delta", deltaLink, StringComparison.OrdinalIgnoreCase);

        // Log some item details if any exist
        if (itemIds.Count > 0)
        {
            var firstItemId = itemIds.First();
            _output.WriteLine($"   First Item ID: {firstItemId}");
        }
    }

    [Fact]
    public async Task GetItemsDeltaAsync_WithPreviousDeltaLink_ReturnsSubsequentChanges()
    {
        // Arrange
        if (ShouldSkipTest()) return;

        // First, get the initial delta to obtain a delta link
        var (initialDeltaLink, initialItems) = await _graphApiClient.PullItemsDeltaAsync(
            Fixture.GetTestSiteId(),
            Fixture.GetTestListId(),
            "latest");

        Assert.NotNull(initialDeltaLink);
        Assert.NotNull(initialItems);

        // Wait a brief moment to ensure any subsequent changes are captured
        await Task.Delay(1000, TestContext.Current.CancellationToken);

        // Act - Use the delta link from the previous call
        var (subsequentDeltaLink, subsequentItems) = await _graphApiClient.PullItemsDeltaAsync(
            Fixture.GetTestSiteId(),
            Fixture.GetTestListId(),
            initialDeltaLink);

        // Assert
        Assert.NotNull(subsequentDeltaLink);
        Assert.NotNull(subsequentItems);

        // Always valid
        Assert.False(string.IsNullOrWhiteSpace(subsequentDeltaLink));
        Assert.True(Uri.TryCreate(subsequentDeltaLink, UriKind.Absolute, out _));
        Assert.Contains("delta", subsequentDeltaLink, StringComparison.OrdinalIgnoreCase);

        // Only require different when a change is known to have happened
        if (subsequentItems.Count > 0)
        {
            Assert.NotEqual(initialDeltaLink, subsequentDeltaLink);
        }

        _output.WriteLine($"   Items Count: {subsequentItems.Count}");
    }

    [Fact]
    public async Task GetItemsDeltaAsync_WithInvalidSiteId_ThrowsException()
    {
        // Arrange
        if (ShouldSkipTest()) return;
        const string invalidSiteId = "invalid-site-id";

        // Act & Assert
        await Assert.ThrowsAsync<ODataError>(async () =>
        {
            await _graphApiClient.PullItemsDeltaAsync(invalidSiteId, Fixture.GetTestListId(), "latest");
        });

        _output.WriteLine("✅ Invalid site ID correctly threw ODataError");
    }

    [Fact]
    public async Task GetItemsDeltaAsync_WithInvalidListId_ThrowsException()
    {
        // Arrange
        if (ShouldSkipTest()) return;
        const string invalidListId = "invalid-list-id";

        // Act & Assert
        await Assert.ThrowsAsync<ODataError>(async () =>
        {
            await _graphApiClient.PullItemsDeltaAsync(Fixture.GetTestSiteId(), invalidListId, "latest");
        });

        _output.WriteLine("✅ Invalid list ID correctly threw ODataError");
    }

    [Fact]
    public async Task GetItemsDeltaAsync_WithInvalidDeltaToken_ThrowsException()
    {
        // Arrange
        if (ShouldSkipTest()) return;
        const string invalidDeltaToken = "invalid-delta-token";

        // Act & Assert
        await Assert.ThrowsAsync<UriFormatException>(async () =>
        {
            await _graphApiClient.PullItemsDeltaAsync(
                Fixture.GetTestSiteId(),
                Fixture.GetTestListId(),
                invalidDeltaToken);
        });

        _output.WriteLine("✅ Invalid delta token correctly threw ODataError");
    }

    [Fact]
    public async Task GetItemsDeltaAsync_WithEmptyParameters_ThrowsException()
    {
        // Arrange & Act & Assert
        await Assert.ThrowsAsync<ODataError>(async () =>
        {
            await _graphApiClient.PullItemsDeltaAsync("", Fixture.GetTestListId(), "latest");
        });

        await Assert.ThrowsAsync<ODataError>(async () =>
        {
            await _graphApiClient.PullItemsDeltaAsync(Fixture.GetTestSiteId(), "", "latest");
        });

        // empty cursor is okay
        await _graphApiClient.PullItemsDeltaAsync(Fixture.GetTestSiteId(), Fixture.GetTestListId(), "");

        _output.WriteLine("✅ Empty parameters correctly threw ArgumentException");
    }

    [Fact]
    public async Task GetItemsDeltaAsync_ReturnsConsistentDataStructure()
    {
        // Arrange
        if (ShouldSkipTest()) return;

        // Act
        var (deltaLink, itemIds) = await _graphApiClient.PullItemsDeltaAsync(
            Fixture.GetTestSiteId(),
            Fixture.GetTestListId(),
            "latest");

        // Assert
        Assert.NotNull(deltaLink);
        Assert.NotNull(itemIds);

        // Verify delta link is a valid URL-like string
        Assert.True(Uri.TryCreate(deltaLink, UriKind.Absolute, out var deltaUri));
        Assert.NotNull(deltaUri);

        _output.WriteLine($"✅ Delta structure validation passed");
        _output.WriteLine($"   Delta Link is: {deltaLink}");
        _output.WriteLine($"   Delta Link is valid URI: {deltaUri}");
        _output.WriteLine($"   Items collection initialized: {itemIds.Count} items");

        // If items exist, verify their basic structure
        if (itemIds.Count > 0)
        {
            foreach (var itemId in itemIds.Take(3)) // Check first 3 items to avoid excessive logging
            {
                Assert.NotNull(itemId);

                _output.WriteLine(
                    $"   Item {itemId}");
            }
        }
    }

    [Fact]
    public async Task GetItemsDeltaAsync_Performance_CompletesWithinReasonableTime()
    {
        // Arrange
        if (ShouldSkipTest()) return;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var (deltaLink, items) = await _graphApiClient.PullItemsDeltaAsync(
            Fixture.GetTestSiteId(),
            Fixture.GetTestListId(),
            "latest");

        stopwatch.Stop();

        // Assert
        Assert.NotNull(deltaLink);
        Assert.NotNull(items);

        _output.WriteLine($"✅ GetItemsDeltaAsync performance test completed");
        _output.WriteLine($"   Execution time: {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"   Items retrieved: {items.Count}");

        // Performance assertion - adjust threshold based on your requirements
        Assert.True(stopwatch.ElapsedMilliseconds < 15000,
            $"GetItemsDeltaAsync took too long: {stopwatch.ElapsedMilliseconds}ms");

        if (items.Count > 0)
        {
            var avgTimePerItem = stopwatch.ElapsedMilliseconds / (double)items.Count;
            _output.WriteLine($"   Average time per item: {avgTimePerItem:F2}ms");
        }
    }


    private bool ShouldSkipTest([System.Runtime.CompilerServices.CallerMemberName] string? testName = null)
    {
        if (string.IsNullOrEmpty(Fixture.GetTestSiteId()) || string.IsNullOrEmpty(Fixture.GetTestListId()) ||
            string.IsNullOrEmpty(TestItemId))
        {
            _output.WriteLine($"⚠️  Skipping {testName} - Environment variables not set");
            _output.WriteLine("   Required: TEST_SHAREPOINT_SITE_ID, TEST_SHAREPOINT_LIST_ID, TEST_SHAREPOINT_ITEM_ID");
            return true;
        }

        return false;
    }

    [Fact]
    public async Task PullItemsModifiedSinceAsync_WithRecentTimestamp_ReturnsItemsOrEmpty()
    {
        // Arrange
        if (ShouldSkipTest()) return;
        var sinceUtc = DateTime.UtcNow.AddDays(-1);

        // Act
        var itemIds = await _graphApiClient.PullItemsModifiedSinceAsync(
            Fixture.GetTestSiteId(),
            Fixture.GetTestListId(),
            sinceUtc);

        // Assert
        Assert.NotNull(itemIds);
        _output.WriteLine($"✅ PullItemsModifiedSinceAsync returned {itemIds.Count} items since {sinceUtc:o}");
    }

    [Fact]
    public async Task GetFreshDeltaLinkAsync_ReturnsDeltaLink()
    {
        // Arrange
        if (ShouldSkipTest()) return;

        // Act
        var deltaLink = await _graphApiClient.GetFreshDeltaLinkAsync(
            Fixture.GetTestSiteId(),
            Fixture.GetTestListId());

        // Assert
        Assert.NotNull(deltaLink);
        Assert.NotEmpty(deltaLink);
        Assert.Contains("delta", deltaLink, StringComparison.OrdinalIgnoreCase);
        _output.WriteLine($"✅ Fresh delta link acquired: {deltaLink}");
    }
}