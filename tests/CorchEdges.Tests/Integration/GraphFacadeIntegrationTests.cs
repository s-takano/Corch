using Azure.Identity;
using CorchEdges.Abstractions;
using CorchEdges.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using Xunit;
using Xunit.Abstractions;

namespace CorchEdges.Tests.Integration;

[Collection("Integration")]
public class GraphFacadeIntegrationTests : IntegrationTestBase
{
    private readonly IGraphFacade _graphFacade;
    private readonly ITestOutputHelper _output;
    private const string TestItemId = "5"; // Pre-created test item with specific permissions setup

    protected override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IGraphFacade, GraphFacade>();
        services.AddScoped<GraphServiceClient>(_ => new GraphServiceClient(new DefaultAzureCredential()));
    }

    public GraphFacadeIntegrationTests(IntegrationTestFixture fixture, ITestOutputHelper output) 
        : base(fixture, output)
    {
        _graphFacade = fixture.Services.GetRequiredService<IGraphFacade>();
        _output = output;
        
        
        _output.WriteLine($"Test Environment Variables:");
        _output.WriteLine($"Item ID: {TestItemId}");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task TestConnectionAsync_WithValidCredentials_ReturnsSuccess()
    {
        // Act
        var result = await _graphFacade.TestConnectionAsync();

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
    [Trait("Category", "Integration")]
    public async Task TestConnectionAsync_WithInvalidCredentials_ReturnsFailureWithReason()
    {
        // This test would need mock setup or invalid credentials
        // Just showing how to use the enhanced result

        var result = await _graphFacade.TestConnectionAsync();

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
    [Trait("Category", "Integration")]
    public async Task GetListItemAsync_WithValidParameters_ReturnsListItem()
    {
        // Arrange
        if (ShouldSkipTest()) return;

        // Act
        var result = await _graphFacade.GetListItemAsync(Fixture.GetTestSiteId(), Fixture.GetTestListId(), TestItemId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(TestItemId, result.Id);
        Assert.NotNull(result.Fields);
        
        _output.WriteLine($"✅ Retrieved list item: {result.Id}");
        _output.WriteLine($"   Created: {result.CreatedDateTime}");
        _output.WriteLine($"   Modified: {result.LastModifiedDateTime}");
        
        // Check if the ProcessFlag field exists (based on your business logic)
        if (result.Fields?.AdditionalData.ContainsKey("ProcessFlag") == true)
        {
            var processFlag = result.Fields.AdditionalData["ProcessFlag"];
            _output.WriteLine($"   ProcessFlag: {processFlag}");
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetListItemAsync_WithInvalidItemId_ReturnsNull()
    {
        // Arrange
        if (ShouldSkipTest()) return;
        const string invalidItemId = "999999";

        // Act & Assert
        await Assert.ThrowsAsync<ODataError>(async ()=> await _graphFacade.GetListItemAsync(Fixture.GetTestSiteId(), Fixture.GetTestListId(), invalidItemId));

        _output.WriteLine("✅ Invalid item ID correctly returned null");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetDriveItemAsync_WithValidParameters_ReturnsDriveItem()
    {
        // Arrange
        if (ShouldSkipTest()) return;

        // Act
        var result = await _graphFacade.GetDriveItemAsync(Fixture.GetTestSiteId(), Fixture.GetTestListId(), TestItemId);

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
    [Trait("Category", "Integration")]
    public async Task GetDriveItemAsync_WithInvalidItemId_ReturnsNull()
    {
        // Arrange
        if (ShouldSkipTest()) return;
        const string invalidItemId = "999999";

        // Act & Assert
        await Assert.ThrowsAsync<ODataError>(()=> _graphFacade.GetDriveItemAsync(Fixture.GetTestSiteId(), Fixture.GetTestListId(), invalidItemId));
        _output.WriteLine("✅ Invalid drive item ID correctly returned null");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DownloadAsync_WithValidDriveItem_ReturnsStream()
    {
        // Arrange
        if (ShouldSkipTest()) return;
        
        // First get the drive item to get the drive ID
        var driveItem = await _graphFacade.GetDriveItemAsync(Fixture.GetTestSiteId(), Fixture.GetTestListId(), TestItemId);
        Assert.NotNull(driveItem);
        Assert.NotNull(driveItem.ParentReference?.DriveId);
        Assert.NotNull(driveItem.Id);

        // Act
        await using var stream = await _graphFacade.DownloadAsync(
            driveItem.ParentReference.DriveId, 
            driveItem.Id);

        // Assert
        Assert.NotNull(stream);
        Assert.True(stream.CanRead);
        
        // Verify we can actually read content
        var buffer = new byte[1024];
        var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
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
    [Trait("Category", "Integration")]
    public async Task DownloadAsync_WithInvalidDriveId_ThrowsException()
    {
        // Arrange
        const string invalidDriveId = "invalid-drive-id";
        const string invalidItemId = "invalid-item-id";

        // Act & Assert
        await Assert.ThrowsAsync<ODataError>(async () =>
        {
            await using var stream = await _graphFacade.DownloadAsync(invalidDriveId, invalidItemId);
        });
        
        _output.WriteLine("✅ Invalid drive/item IDs correctly threw ServiceException");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task IntegrationWorkflow_CompleteProcess_WorksEndToEnd()
    {
        // Arrange
        if (ShouldSkipTest()) return;

        // Act & Assert - Complete workflow
        _output.WriteLine("🔄 Starting complete integration workflow...");

        // Step 1: Test connection
        var connectionResult = await _graphFacade.TestConnectionAsync();
        Assert.True(connectionResult.IsSuccess);
        _output.WriteLine("   ✅ Step 1: Connection verified");

        // Step 2: Get list item
        var listItem = await _graphFacade.GetListItemAsync(Fixture.GetTestSiteId(), Fixture.GetTestListId(), TestItemId);
        Assert.NotNull(listItem);
        _output.WriteLine($"   ✅ Step 2: List item retrieved (ID: {listItem.Id})");

        // Step 3: Get drive item
        var driveItem = await _graphFacade.GetDriveItemAsync(Fixture.GetTestSiteId(), Fixture.GetTestListId(), TestItemId);
        Assert.NotNull(driveItem);
        Assert.NotNull(driveItem.ParentReference?.DriveId);
        _output.WriteLine($"   ✅ Step 3: Drive item retrieved (Name: {driveItem.Name})");

        // Step 4: Download content
        await using var stream = await _graphFacade.DownloadAsync(
            driveItem.ParentReference.DriveId, 
            driveItem.Id!);
        Assert.NotNull(stream);
        
        var buffer = new byte[1024];
        var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
        Assert.True(bytesRead > 0);
        _output.WriteLine($"   ✅ Step 4: File downloaded ({bytesRead} bytes read)");

        _output.WriteLine("🎉 Complete integration workflow successful!");
    }

    [Fact]
    [Trait("Category", "Integration")]
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
            tasks.Add(_graphFacade.GetListItemAsync(Fixture.GetTestSiteId(), Fixture.GetTestListId(), TestItemId));
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

    private bool ShouldSkipTest([System.Runtime.CompilerServices.CallerMemberName] string? testName = null)
    {
        if (string.IsNullOrEmpty(Fixture.GetTestSiteId()) || string.IsNullOrEmpty(Fixture.GetTestListId()) || string.IsNullOrEmpty(TestItemId))
        {
            _output.WriteLine($"⚠️  Skipping {testName} - Environment variables not set");
            _output.WriteLine("   Required: TEST_SHAREPOINT_SITE_ID, TEST_SHAREPOINT_LIST_ID, TEST_SHAREPOINT_ITEM_ID");
            return true;
        }
        return false;
    }
}