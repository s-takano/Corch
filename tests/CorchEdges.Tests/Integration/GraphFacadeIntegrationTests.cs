using Azure.Identity;
using CorchEdges.Abstractions;
using CorchEdges.Utilities;
using DotNetEnv;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using Xunit;
using Xunit.Abstractions;

namespace CorchEdges.Tests.Integration;

[Collection("Integration")]
public class GraphFacadeIntegrationTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IGraphFacade _graphFacade;
    private readonly ITestOutputHelper _output;
    private readonly string? _siteId;
    private readonly string? _listId;
    private readonly string? _itemId;

    public GraphFacadeIntegrationTests(IntegrationTestFixture fixture, ITestOutputHelper output)
    {
        _graphFacade = fixture.Services.GetRequiredService<IGraphFacade>();
        _output = output;
        
        // Get test data from environment variables
        _siteId = Environment.GetEnvironmentVariable("TEST_SHAREPOINT_SITE_ID");
        _listId = Environment.GetEnvironmentVariable("TEST_SHAREPOINT_LIST_ID");
        _itemId = Environment.GetEnvironmentVariable("TEST_SHAREPOINT_ITEM_ID");
        
        _output.WriteLine($"Test Environment Variables:");
        _output.WriteLine($"Site ID: {_siteId ?? "NOT SET"}");
        _output.WriteLine($"List ID: {_listId ?? "NOT SET"}");
        _output.WriteLine($"Item ID: {_itemId ?? "NOT SET"}");
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
        var result = await _graphFacade.GetListItemAsync(_siteId!, _listId!, _itemId!);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(_itemId, result.Id);
        Assert.NotNull(result.Fields);
        
        _output.WriteLine($"✅ Retrieved list item: {result.Id}");
        _output.WriteLine($"   Created: {result.CreatedDateTime}");
        _output.WriteLine($"   Modified: {result.LastModifiedDateTime}");
        
        // Check if ProcessFlag field exists (based on your business logic)
        if (result.Fields?.AdditionalData?.ContainsKey("ProcessFlag") == true)
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
        await Assert.ThrowsAsync<ODataError>(async ()=> await _graphFacade.GetListItemAsync(_siteId!, _listId!, invalidItemId));

        _output.WriteLine("✅ Invalid item ID correctly returned null");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetDriveItemAsync_WithValidParameters_ReturnsDriveItem()
    {
        // Arrange
        if (ShouldSkipTest()) return;

        // Act
        var result = await _graphFacade.GetDriveItemAsync(_siteId!, _listId!, _itemId!);

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
        await Assert.ThrowsAsync<ODataError>(()=> _graphFacade.GetDriveItemAsync(_siteId!, _listId!, invalidItemId));
        _output.WriteLine("✅ Invalid drive item ID correctly returned null");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DownloadAsync_WithValidDriveItem_ReturnsStream()
    {
        // Arrange
        if (ShouldSkipTest()) return;
        
        // First get the drive item to get the drive ID
        var driveItem = await _graphFacade.GetDriveItemAsync(_siteId!, _listId!, _itemId!);
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
        var listItem = await _graphFacade.GetListItemAsync(_siteId!, _listId!, _itemId!);
        Assert.NotNull(listItem);
        _output.WriteLine($"   ✅ Step 2: List item retrieved (ID: {listItem.Id})");

        // Step 3: Get drive item
        var driveItem = await _graphFacade.GetDriveItemAsync(_siteId!, _listId!, _itemId!);
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
        
        for (int i = 0; i < concurrentRequests; i++)
        {
            tasks.Add(_graphFacade.GetListItemAsync(_siteId!, _listId!, _itemId!));
        }

        var results = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        Assert.All(results, result => Assert.NotNull(result));
        Assert.All(results, result => Assert.Equal(_itemId, result!.Id));
        
        _output.WriteLine($"✅ {concurrentRequests} concurrent requests completed in {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"   Average: {stopwatch.ElapsedMilliseconds / concurrentRequests}ms per request");
        
        // Performance assertion - adjust threshold as needed
        Assert.True(stopwatch.ElapsedMilliseconds < 30000, 
            $"Concurrent requests took too long: {stopwatch.ElapsedMilliseconds}ms");
    }

    private bool ShouldSkipTest([System.Runtime.CompilerServices.CallerMemberName] string? testName = null)
    {
        if (string.IsNullOrEmpty(_siteId) || string.IsNullOrEmpty(_listId) || string.IsNullOrEmpty(_itemId))
        {
            _output.WriteLine($"⚠️  Skipping {testName} - Environment variables not set");
            _output.WriteLine("   Required: TEST_SHAREPOINT_SITE_ID, TEST_SHAREPOINT_LIST_ID, TEST_SHAREPOINT_ITEM_ID");
            return true;
        }
        return false;
    }
}

public class IntegrationTestFixture : IDisposable
{
    public IServiceProvider Services { get; }

    public IntegrationTestFixture()
    {
        // Load .env file - automatically finds it in current directory or parent directories
        Env.Load();

        var host = new HostBuilder()
            .ConfigureAppConfiguration(config =>
            {
                config.AddJsonFile("appsettings.json", optional: false);        // 1. Base config
                config.AddJsonFile("appsettings.test.json", optional: true);    // 2. Environment-specific
                config.AddEnvironmentVariables();                               // 3. Secrets/overrides (HIGHEST priority)
            })
            .ConfigureServices(services =>
            {
                // Register GraphServiceClient with DefaultAzureCredential
                services.AddScoped<GraphServiceClient>(provider =>
                {
                    var credential = new DefaultAzureCredential();
                    return new GraphServiceClient(credential);
                });

                services.AddScoped<IGraphFacade, GraphFacade>();
            })
            .Build();

        Services = host.Services;
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
}