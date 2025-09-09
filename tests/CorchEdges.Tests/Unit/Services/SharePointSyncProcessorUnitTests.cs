using System.Data;
using System.Data.Common;
using CorchEdges.Abstractions;
using CorchEdges.Data;
using CorchEdges.Data.Abstractions;
using CorchEdges.Data.Entities;
using CorchEdges.Data.Repositories;
using CorchEdges.Data.Utilities;
using CorchEdges.Models;
using CorchEdges.Services;
using CorchEdges.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Models;
using Moq;

namespace CorchEdges.Tests.Unit.Services;

[Trait("Category", TestCategories.Unit)]
public class SharePointSyncProcessorUnitTests : IDisposable
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly Mock<IGraphApiClient> _mockGraph;
    private readonly Mock<ITabularDataParser> _mockParser;
    private readonly Mock<IDatabaseWriter> _mockDb;
    private readonly EdgesDbContext _mockContext;
    private readonly ProcessingLogRepository _mockProcessingLogRepository;
    private readonly Mock<IProcessingLogRepository> _mockProcessingLogRepository2;
    private readonly string _testSiteId = "12345678-1234-1234-1234-123456789012";
    private readonly string _testListId = "87654321-4321-4321-4321-210987654321";
    private readonly Mock<IDataSetConverter> _mockDataSetConverter;
    private readonly Mock<IProcessedFileRepository> _mockProcessedFileRepository;

    public SharePointSyncProcessorUnitTests()
    {
        _mockLogger = new Mock<ILogger>();
        _mockGraph = new Mock<IGraphApiClient>();
        _mockParser = new Mock<ITabularDataParser>();
        _mockDb = new Mock<IDatabaseWriter>();
        _mockContext = MemoryDatabaseTestBase.CreateInMemoryDbContext();
        _mockProcessingLogRepository = new ProcessingLogRepository(_mockContext);
        _mockProcessingLogRepository2 = new Mock<IProcessingLogRepository>();
        _mockProcessedFileRepository = new Mock<IProcessedFileRepository>();
        _mockDataSetConverter = new Mock<IDataSetConverter>();
    }


    private SharePointSyncProcessor CreateHandlerWithProcessingLogRepository(string watchedPath = "/sites/test/Shared Documents/WatchedFolder")
    {
        return new SharePointSyncProcessor(
            _mockLogger.Object,
            _mockGraph.Object,
            _mockParser.Object,
            _mockDb.Object,
            _mockContext,
            _mockProcessingLogRepository,
            _mockProcessedFileRepository.Object,
            _mockDataSetConverter.Object,
            _testSiteId,
            _testListId,
            watchedPath);
    }

    
    private SharePointSyncProcessor CreateHandler(string watchedPath = "/Shared Documents/WatchedFolder")
    {
        return new SharePointSyncProcessor(
            _mockLogger.Object,
            _mockGraph.Object,
            _mockParser.Object,
            _mockDb.Object,
            _mockContext,
            _mockProcessingLogRepository2.Object,
            _mockProcessedFileRepository.Object,
            _mockDataSetConverter.Object,
            _testSiteId,
            _testListId,
            watchedPath);
    }
    
    [Fact]
    public void Constructor_WithValidParameters_ShouldNotThrow()
    {
        // Arrange & Act
        var handler = CreateHandler();

        // Assert
        handler.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new SharePointSyncProcessor(
                null!,
                _mockGraph.Object,
                _mockParser.Object,
                _mockDb.Object,
                _mockContext,
                _mockProcessingLogRepository2.Object,
                _mockProcessedFileRepository.Object,
                _mockDataSetConverter.Object,
                _testSiteId,
                _testListId,
                "/test/path"));
    }

    [Fact]
    public void Constructor_WithNullGraph_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new SharePointSyncProcessor(
                _mockLogger.Object,
                null!,
                _mockParser.Object,
                _mockDb.Object,
                _mockContext,
                _mockProcessingLogRepository2.Object,
                _mockProcessedFileRepository.Object,
                _mockDataSetConverter.Object,
                _testSiteId,
                _testListId,
                "/test/path"));
    }

    [Fact]
    public void Constructor_WithNullParser_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new SharePointSyncProcessor(
                _mockLogger.Object,
                _mockGraph.Object,
                null!,
                _mockDb.Object,
                _mockContext,
                _mockProcessingLogRepository2.Object,
                _mockProcessedFileRepository.Object,
                _mockDataSetConverter.Object,
                _testSiteId,
                _testListId,
                "/test/path"));
    }

    [Fact]
    public void Constructor_WithNullDatabaseWriter_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new SharePointSyncProcessor(
                _mockLogger.Object,
                _mockGraph.Object,
                _mockParser.Object,
                null!,
                _mockContext,
                _mockProcessingLogRepository2.Object,
                _mockProcessedFileRepository.Object,
                _mockDataSetConverter.Object,
                _testSiteId,
                _testListId,
                "/test/path"));
    }

    [Fact]
    public void Constructor_WithNullContext_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new SharePointSyncProcessor(
                _mockLogger.Object,
                _mockGraph.Object,
                _mockParser.Object,
                _mockDb.Object,
                null!,
                _mockProcessingLogRepository2.Object,
                _mockProcessedFileRepository.Object,
                _mockDataSetConverter.Object,
                _testSiteId,
                _testListId,
                "/test/path"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void Constructor_WithInvalidSiteId_ShouldThrowArgumentException(string? invalidSiteId)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new SharePointSyncProcessor(
                _mockLogger.Object,
                _mockGraph.Object,
                _mockParser.Object,
                _mockDb.Object,
                _mockContext,
                _mockProcessingLogRepository2.Object,
                _mockProcessedFileRepository.Object,
                _mockDataSetConverter.Object,
                invalidSiteId!,
                _testListId,
                "/test/path"));

        exception.ParamName.Should().Be("siteId");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void Constructor_WithInvalidListId_ShouldThrowArgumentException(string? invalidListId)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new SharePointSyncProcessor(
                _mockLogger.Object,
                _mockGraph.Object,
                _mockParser.Object,
                _mockDb.Object,
                _mockContext,
                _mockProcessingLogRepository2.Object,
                _mockProcessedFileRepository.Object,
                _mockDataSetConverter.Object,
                _testSiteId,
                invalidListId!,
                "/test/path"));

        exception.ParamName.Should().Be("listId");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void Constructor_WithInvalidWatchedPath_ShouldThrowArgumentException(string? invalidWatchedPath)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new SharePointSyncProcessor(
                _mockLogger.Object,
                _mockGraph.Object,
                _mockParser.Object,
                _mockDb.Object,
                _mockContext,
                _mockProcessingLogRepository2.Object,
                _mockProcessedFileRepository.Object,
                _mockDataSetConverter.Object,
                _testSiteId,
                _testListId,
                invalidWatchedPath!));

        exception.ParamName.Should().Be("watchedPath");
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("12345")]
    [InlineData("invalid-format")]
    public void Constructor_WithInvalidSiteIdFormat_ShouldThrowArgumentException(string invalidSiteId)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new SharePointSyncProcessor(
                _mockLogger.Object,
                _mockGraph.Object,
                _mockParser.Object,
                _mockDb.Object,
                _mockContext,
                _mockProcessingLogRepository2.Object,
                _mockProcessedFileRepository.Object,
                _mockDataSetConverter.Object,
                invalidSiteId,
                _testListId,
                "/test/path"));

        exception.Message.Should().Contain("must be a valid GUID or SharePoint ID format");
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("12345")]
    [InlineData("invalid-format")]
    public void Constructor_WithInvalidListIdFormat_ShouldThrowArgumentException(string invalidListId)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new SharePointSyncProcessor(
                _mockLogger.Object,
                _mockGraph.Object,
                _mockParser.Object,
                _mockDb.Object,
                _mockContext,
                _mockProcessingLogRepository2.Object,
                _mockProcessedFileRepository.Object,
                _mockDataSetConverter.Object,
                _testSiteId,
                invalidListId,
                "/test/path"));

        exception.Message.Should().Contain("must be a valid GUID");
    }

    [Fact]
    public void Constructor_WithSharePointCompositeId_ShouldNotThrow()
    {
        // Arrange
        var sharePointSiteId =
            "contoso.sharepoint.com,12345678-1234-1234-1234-123456789012,87654321-4321-4321-4321-210987654321";

        // Act & Assert
        var handler = new SharePointSyncProcessor(
            _mockLogger.Object,
            _mockGraph.Object,
            _mockParser.Object,
            _mockDb.Object,
            _mockContext,
            _mockProcessingLogRepository2.Object,
            _mockProcessedFileRepository.Object,
            _mockDataSetConverter.Object,
            sharePointSiteId,
            _testListId,
            "/test/path");

        handler.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleAsync_WithItemInWatchedFolder_ShouldProcessItem()
    {
        // Arrange
        var watchedPath = "/sites/test/Shared Documents/WatchedFolder";
        var handler = CreateHandler(watchedPath);

        var changeNotification = new SharePointNotification
        {
            Resource = "sites/test-site/lists/test-list/items/123",
            ChangeType = nameof(ChangeType.Updated)
        };

        var mockDriveItem = new DriveItem
        {
            Id = "test-item-id",
            Name = "test-file.xlsx",
            ParentReference = new ItemReference
            {
                Path = "/sites/test/Shared Documents/WatchedFolder"
            }
        };

        _mockGraph.Setup(g => g.GetListItemAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new ListItem { Id = "123" });

        _mockGraph.Setup(g => g.GetDriveItemAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync(mockDriveItem);

        _mockGraph.Setup(g => g.PullItemsDeltaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(("delta", new List<string> { "test-item-id" }));

        _mockParser.Setup(p => p.Parse(It.IsAny<Stream>()))
            .Returns((new DataSet(), string.Empty));

        _mockGraph.Setup(g => g.DownloadAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new MemoryStream());

        // Setup processing log repository mock
        _mockProcessingLogRepository2.Setup(x => x.GetDeltaLinkForSyncAsync(_testSiteId, _testListId))
            .ReturnsAsync("test-delta-link");

        _mockProcessedFileRepository.Setup(x => x.GetByIdAsync(It.IsAny<int>()))
            .ReturnsAsync(new ProcessedFile());
        
        // Act
        var notificationList = ((IEnumerable<SharePointNotification>) [changeNotification]).ToList();
        foreach (var unused in notificationList) await handler.FetchAndStoreDeltaAsync();

        // Assert
        _mockGraph.Verify(g => g.GetListItemAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithItemOutsideWatchedFolder_ShouldSkipProcessing()
    {
        // Arrange
        var watchedPath = "/sites/test/Shared Documents/WatchedFolder";
        var handler = CreateHandler(watchedPath);

        var changeNotification = new SharePointNotification
        {
            Resource = "sites/test-site/lists/test-list/items/123",
            ChangeType = nameof(ChangeType.Updated)
        };

        var mockDriveItem = new DriveItem
        {
            Id = "test-item-id",
            Name = "test-file.xlsx",
            ParentReference = new ItemReference
            {
                Path = "/sites/test/Shared Documents/UnwatchedFolder"
            }
        };

        _mockGraph.Setup(g => g.GetListItemAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new ListItem { Id = "123" });

        _mockGraph.Setup(g => g.GetDriveItemAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync(mockDriveItem);

        _mockGraph.Setup(g => g.PullItemsDeltaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(("delta", new List<string> { "test-item-id" }));

        // Setup processing log repository mock
        _mockProcessingLogRepository2.Setup(x => x.GetDeltaLinkForSyncAsync(_testSiteId, _testListId))
            .ReturnsAsync("test-delta-link");

        // Act
        var notificationList = ((IEnumerable<SharePointNotification>) [changeNotification]).ToList();
        foreach (var unused in notificationList) await handler.FetchAndStoreDeltaAsync();

        // Assert
        VerifyLogMessage("Skipping item outside watched folder", Times.Once());
        _mockParser.Verify(p => p.Parse(It.IsAny<Stream>()), Times.Never);
        _mockDb.Verify(db => db.WriteAsync(
            It.IsAny<DataSet>(),
            It.IsAny<EdgesDbContext>(),
            It.IsAny<DbConnection>(),
            It.IsAny<DbTransaction>()), Times.Never);
    }

    [Theory]
    [InlineData("/sites/test/Shared Documents/WatchedFolder",
        "/sites/test/Shared Documents/WatchedFolder/",
        true)]
    [InlineData("/sites/test/Shared Documents/WatchedFolder",
        "/sites/test/Shared Documents/WatchedFolder/subfolder/",
        false)]
    [InlineData("/sites/test/Shared Documents/WatchedFolder",
        "/sites/test/Shared Documents/OtherFolder/",
        false)]
    [InlineData("/sites/test/Shared Documents/WatchedFolder",
        "/sites/test/Shared Documents/",
        false)]
    [InlineData("/sites/test/Shared Documents/WatchedFolder",
        "/sites/different/Shared Documents/WatchedFolder/",
        false)]
    public async Task HandleAsync_WithPathFilter_ShouldProcessOrSkipBasedOnLocation(
        string watchedPath,
        string itemParentPath,
        bool shouldProcess)
    {
        // Arrange
        var handler = CreateHandler(watchedPath);

        var changeNotification = new SharePointNotification
        {
            Resource = "sites/test-site/lists/test-list/items/123",
            ChangeType = nameof(ChangeType.Updated)
        };

        var mockDriveItem = new DriveItem
        {
            Id = "test-item-id",
            Name = "test-file.xlsx",
            ParentReference = new ItemReference { Path = itemParentPath }
        };

        _mockGraph.Setup(g => g.GetListItemAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new ListItem { Id = "123" });

        _mockGraph.Setup(g => g.GetDriveItemAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync(mockDriveItem);

        _mockGraph.Setup(g => g.PullItemsDeltaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(("delta", new List<string> { "test-item-id" }));

        // Setup processing log repository mock
        _mockProcessingLogRepository2.Setup(x => x.GetDeltaLinkForSyncAsync(_testSiteId, _testListId))
            .ReturnsAsync("test-delta-link");

        if (shouldProcess)
        {
            _mockParser.Setup(p => p.Parse(It.IsAny<Stream>()))
                .Returns((new DataSet(), string.Empty));
            _mockGraph.Setup(g => g.DownloadAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new MemoryStream());
        }
        _mockProcessedFileRepository.Setup(x => x.GetByIdAsync(It.IsAny<int>()))
            .ReturnsAsync(new ProcessedFile());

        // Act
        var notificationList = ((IEnumerable<SharePointNotification>) [changeNotification]).ToList();
        foreach (var unused in notificationList) await handler.FetchAndStoreDeltaAsync();

        // Assert
        if (shouldProcess)
        {
            VerifyLogMessage("Skipping item outside watched folder", Times.Never());
        }
        else
        {
            VerifyLogMessage("Skipping item outside watched folder", Times.Once());
            _mockParser.Verify(p => p.Parse(It.IsAny<Stream>()), Times.Never);
        }
    }

    [Theory]
    [InlineData("/sites/test/Shared Documents/WatchedFolder", "/sites/test/shared documents/watchedfolder")]
    [InlineData("/Sites/Test/Shared Documents/WatchedFolder", "/sites/test/Shared Documents/WatchedFolder")]
    [InlineData("/sites/test/Shared Documents/WatchedFolder/", "/sites/test/Shared Documents/WatchedFolder")]
    public async Task HandleAsync_WithCaseInsensitivePaths_ShouldMatch(string watchedPath, string itemParentPath)
    {
        // Arrange
        var handler = CreateHandler(watchedPath);

        var changeNotification = new SharePointNotification
        {
            Resource = "sites/test-site/lists/test-list/items/123",
            ChangeType = nameof(ChangeType.Updated)
        };

        var mockDriveItem = new DriveItem
        {
            Id = "test-item-id",
            Name = "test-file.xlsx",
            ParentReference = new ItemReference { Path = itemParentPath }
        };

        _mockGraph.Setup(g => g.GetListItemAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new ListItem { Id = "123" });

        _mockGraph.Setup(g => g.GetDriveItemAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync(mockDriveItem);

        _mockGraph.Setup(g => g.PullItemsDeltaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(("delta", new List<string> { "test-item-id" }));

        // Setup processing log repository mock
        _mockProcessingLogRepository2.Setup(x => x.GetDeltaLinkForSyncAsync(_testSiteId, _testListId))
            .ReturnsAsync("test-delta-link");

        _mockParser.Setup(p => p.Parse(It.IsAny<Stream>()))
            .Returns((new DataSet(), string.Empty));
        _mockGraph.Setup(g => g.DownloadAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new MemoryStream());
        _mockProcessedFileRepository.Setup(x => x.GetByIdAsync(It.IsAny<int>()))
            .ReturnsAsync(new ProcessedFile());

        // Act
        var notificationList = ((IEnumerable<SharePointNotification>) [changeNotification]).ToList();
        foreach (var unused in notificationList) await handler.FetchAndStoreDeltaAsync();

        // Assert
        VerifyLogMessage("Skipping item outside watched folder", Times.Never());
    }

    [Fact]
    public async Task EnsureGraphConnectionAsync_ShouldReturnGraphConnectionResult()
    {
        // Arrange
        var handler = CreateHandler();
        var connectionResult = new ConnectionTestResult(true);
        _mockGraph.Setup(g => g.TestConnectionAsync("root")).ReturnsAsync(connectionResult);

        // Act
        var result = await handler.EnsureGraphConnectionAsync();

        // Assert
        result.Should().BeTrue();
        _mockGraph.Verify(g => g.TestConnectionAsync("root"), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenProcessingSucceeds_ShouldStoreProcessingLogRecord()
    {
        // Arrange
        var handler = CreateHandlerWithProcessingLogRepository();
        var testSiteId = _testSiteId;
        var testListId = _testListId;

        var changeNotification = new SharePointNotification
        {
            Resource = "sites/test-site/lists/test-list/items/123",
            ChangeType = nameof(ChangeType.Updated)
        };

        var mockDriveItem = new DriveItem
        {
            Id = "test-item-id",
            Name = "test-file.xlsx",
            ParentReference = new ItemReference
            {
                Path = "/sites/test/Shared Documents/WatchedFolder",
                DriveId = "test-drive-id"
            }
        };

        _mockGraph.Setup(g => g.GetListItemAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new ListItem { Id = "123" });

        _mockGraph.Setup(g => g.GetDriveItemAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(mockDriveItem);

        _mockGraph.Setup(g => g.PullItemsDeltaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(("delta", new List<string> { "test-item-id" }));

        _mockParser.Setup(p => p.Parse(It.IsAny<Stream>()))
            .Returns((new DataSet(), string.Empty));

        _mockGraph.Setup(g => g.DownloadAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new MemoryStream());
        _mockProcessedFileRepository.Setup(x => x.GetByIdAsync(It.IsAny<int>()))
            .ReturnsAsync(new ProcessedFile());

        // Act
        var notificationList = ((IEnumerable<SharePointNotification>) [changeNotification]).ToList();
        foreach (var unused in notificationList) await handler.FetchAndStoreDeltaAsync();

        // Assert
        var processingLogs = await _mockContext.ProcessingLogs.ToListAsync(TestContext.Current.CancellationToken);

        processingLogs.Should().HaveCount(1);
        var log = processingLogs.First();

        log.SiteId.Should().Be(testSiteId);
        log.ListId.Should().Be(testListId);
        log.Status.Should().Be("Completed");
        log.LastProcessedAt.Should().BeCloseTo(DatabaseDateTime.UtcNow, TimeSpan.FromMinutes(1));
        log.SuccessfulItems.Should().Be(1);
        log.FailedItems.Should().Be(0);
        log.LastProcessedCount.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_WhenProcessingFails_ShouldStoreProcessingLogWithError()
    {
        // Arrange
        var handler = CreateHandlerWithProcessingLogRepository();

        var changeNotification = new SharePointNotification
        {
            Resource = "sites/test-site/lists/test-list/items/123",
            ChangeType = nameof(ChangeType.Updated)
        };

        var mockDriveItem = new DriveItem
        {
            Id = "test-item-id",
            Name = "test-file.xlsx",
            ParentReference = new ItemReference
            {
                Path = "/sites/test/Shared Documents/WatchedFolder",
                DriveId = "test-drive-id"
            }
        };

        _mockGraph.Setup(g => g.GetListItemAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new ListItem { Id = "123" });

        _mockGraph.Setup(g => g.GetDriveItemAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(mockDriveItem);

        _mockGraph.Setup(g => g.DownloadAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new MemoryStream());

        _mockGraph.Setup(g => g.PullItemsDeltaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(("delta", new List<string> { "test-item-id" }));

        _mockParser.Setup(p => p.Parse(It.IsAny<Stream>()))
            .Returns((null, "Test parsing error"));


        // Act
        var notificationList = ((IEnumerable<SharePointNotification>) [changeNotification]).ToList();
        foreach (var unused in notificationList) await handler.FetchAndStoreDeltaAsync();

        // Assert
        var processingLogs = await _mockContext.ProcessingLogs.ToListAsync(TestContext.Current.CancellationToken);

        processingLogs.Should().HaveCount(1);
        var log = processingLogs.First();

        log.SiteId.Should().Be(_testSiteId);
        log.ListId.Should().Be(_testListId);
        log.Status.Should().Be("Failed");
        log.LastError.Should().Be("Test parsing error");
        log.FailedItems.Should().Be(1);
        log.SuccessfulItems.Should().Be(0);
    }

    [Fact]
    public async Task FetchAndStoreItemAsync_WithDuplicateFile_ShouldSkipProcessing()
    {
        // Arrange
        var handler = CreateHandler();
        var itemId = "test-item-id";
        var fileName = "test.xlsx";

        // Mock list item with ProcessFlag = "Yes"
        var listItem = new ListItem
        {
            Fields = new FieldValueSet
            {
                AdditionalData = new Dictionary<string, object> { { "ProcessFlag", "Yes" } }
            }
        };

        // Mock drive item
        var driveItem = new DriveItem
        {
            Id = itemId,
            Name = fileName,
            ParentReference = new ItemReference
            {
                Path = "/sites/test/drive/root:/Shared Documents/WatchedFolder",
                DriveId = "drive-123"
            }
        };

        // Mock file content stream
        var fileContent = System.Text.Encoding.UTF8.GetBytes("sample excel content");
        var fileStream = new MemoryStream(fileContent);

        // Setup mocks
        _mockGraph.Setup(x => x.GetListItemAsync(_testSiteId, _testListId, itemId))
            .ReturnsAsync(listItem);
        _mockGraph.Setup(x => x.GetDriveItemAsync(_testSiteId, _testListId, itemId))
            .ReturnsAsync(driveItem);
        _mockGraph.Setup(x => x.DownloadAsync("drive-123", itemId))
            .ReturnsAsync(fileStream);

        // Mock ProcessedFileRepository to return true for duplicate check
        _mockProcessedFileRepository.Setup(x => x.ExistsByHashAsync(It.IsAny<string>(), It.IsAny<long>()))
            .ReturnsAsync(true);

        // Setup other required mocks
        _mockProcessingLogRepository2.Setup(x => x.GetDeltaLinkForSyncAsync(_testSiteId, _testListId))
            .ReturnsAsync("test-delta-link");
        _mockGraph.Setup(x => x.PullItemsDeltaAsync(_testSiteId, _testListId, "test-delta-link"))
            .ReturnsAsync(("new-delta-link", new List<string> { itemId }));

        // Act
        var result = await handler.FetchAndStoreDeltaAsync();

        // Assert
        result.Success.Should().BeTrue();
        handler.SuccessfulItems.Should().Be(0); // No items should be processed due to duplication
        handler.FailedCount.Should().Be(0);

        // Verify that duplicate check was called
        _mockProcessedFileRepository.Verify(x => x.ExistsByHashAsync(It.IsAny<string>(), It.IsAny<long>()), Times.Once);

        // Verify that parser and database writer were never called since file was skipped
        _mockParser.Verify(x => x.Parse(It.IsAny<Stream>()), Times.Never);
        _mockDb.Verify(x => x.WriteAsync(It.IsAny<DataSet>(), It.IsAny<EdgesDbContext>(),
            It.IsAny<DbConnection>(), It.IsAny<DbTransaction>()), Times.Never);

        // Verify log message for duplicate detection
        VerifyLogMessage("Duplicate file detected with hash", Times.Once());
    }

    [Fact]
    public async Task FetchAndStoreItemAsync_WithNonDuplicateFile_ShouldProcessFile()
    {
        // Arrange
        var handler = CreateHandler();
        var itemId = "test-item-id";
        var fileName = "test.xlsx";

        // Mock list item with ProcessFlag = "Yes"
        var listItem = new ListItem
        {
            Fields = new FieldValueSet
            {
                AdditionalData = new Dictionary<string, object> { { "ProcessFlag", "Yes" } }
            }
        };

        // Mock drive item
        var driveItem = new DriveItem
        {
            Id = itemId,
            Name = fileName,
            ParentReference = new ItemReference
            {
                Path = "/sites/test/drive/root:/Shared Documents/WatchedFolder",
                DriveId = "drive-123"
            }
        };

        // Mock file content stream
        var fileContent = System.Text.Encoding.UTF8.GetBytes("sample excel content");
        var fileStream = new MemoryStream(fileContent);

        // Mock successful parsing
        var mockDataSet = new DataSet();
        var mockPreparedDataSet = new DataSet();

        // Setup mocks
        _mockGraph.Setup(x => x.GetListItemAsync(_testSiteId, _testListId, itemId))
            .ReturnsAsync(listItem);
        _mockGraph.Setup(x => x.GetDriveItemAsync(_testSiteId, _testListId, itemId))
            .ReturnsAsync(driveItem);
        _mockGraph.Setup(x => x.DownloadAsync("drive-123", itemId))
            .ReturnsAsync(fileStream);

        // Mock ProcessedFileRepository to return false for duplicate check (not a duplicate)
        _mockProcessedFileRepository.Setup(x => x.ExistsByHashAsync(It.IsAny<string>(), It.IsAny<long>()))
            .ReturnsAsync(false);

        // Mock successful parsing
        _mockParser.Setup(x => x.Parse(It.IsAny<Stream>()))
            .Returns((mockDataSet, null));
        _mockDataSetConverter.Setup(x => x.ConvertForDatabase(mockDataSet))
            .Returns(mockPreparedDataSet);

        // Setup other required mocks
        _mockProcessingLogRepository2.Setup(x => x.GetDeltaLinkForSyncAsync(_testSiteId, _testListId))
            .ReturnsAsync("test-delta-link");
        _mockGraph.Setup(x => x.PullItemsDeltaAsync(_testSiteId, _testListId, "test-delta-link"))
            .ReturnsAsync(("new-delta-link", new List<string> { itemId }));

        _mockProcessedFileRepository.Setup(x => x.GetByIdAsync(It.IsAny<int>()))
            .ReturnsAsync(new ProcessedFile());

        // Act
        var result = await handler.FetchAndStoreDeltaAsync();

        // Assert
        result.Success.Should().BeTrue();
        handler.SuccessfulItems.Should().Be(1); // File should be processed successfully
        handler.FailedCount.Should().Be(0);

        // Verify that duplicate check was called
        _mockProcessedFileRepository.Verify(x => x.ExistsByHashAsync(It.IsAny<string>(), It.IsAny<long>()), Times.Once);

        // Verify that parser and database writer were called since file was not a duplicate
        _mockParser.Verify(x => x.Parse(It.IsAny<Stream>()), Times.Once);
        _mockDb.Verify(x => x.WriteAsync(mockPreparedDataSet, It.IsAny<EdgesDbContext>(),
            It.IsAny<DbConnection>(), It.IsAny<DbTransaction>()), Times.Once);

        // Verify no duplicate log message was logged
        VerifyLogMessage("Duplicate file detected with hash", Times.Never());
    }

    [Theory]
    [InlineData(1024L)]
    [InlineData(2048L)]
    [InlineData(0L)]
    public async Task FetchAndStoreItemAsync_WithVariousHashAndSizeValues_ShouldCallDuplicateCheckCorrectly(
        long expectedSize)
    {
        // Arrange
        var handler = CreateHandler();
        var itemId = "test-item-id";
        var fileName = "test.xlsx";

        // Mock list item with ProcessFlag = "Yes"
        var listItem = new ListItem
        {
            Fields = new FieldValueSet
            {
                AdditionalData = new Dictionary<string, object> { { "ProcessFlag", "Yes" } }
            }
        };

        // Mock drive item
        var driveItem = new DriveItem
        {
            Id = itemId,
            Name = fileName,
            ParentReference = new ItemReference
            {
                Path = "/sites/test/drive/root:/Shared Documents/WatchedFolder",
                DriveId = "drive-123"
            }
        };

        // Create file content that would produce the expected hash and size
        var fileContent = new byte[expectedSize];
        var fileStream = new MemoryStream(fileContent);

        // Setup mocks
        _mockGraph.Setup(x => x.GetListItemAsync(_testSiteId, _testListId, itemId))
            .ReturnsAsync(listItem);
        _mockGraph.Setup(x => x.GetDriveItemAsync(_testSiteId, _testListId, itemId))
            .ReturnsAsync(driveItem);
        _mockGraph.Setup(x => x.DownloadAsync("drive-123", itemId))
            .ReturnsAsync(fileStream);

        // Mock ProcessedFileRepository to return false (not duplicate) to continue processing
        _mockProcessedFileRepository.Setup(x => x.ExistsByHashAsync(It.IsAny<string>(), It.IsAny<long>()))
            .ReturnsAsync(false);

        // Mock successful parsing to avoid errors
        var mockDataSet = new DataSet();
        _mockParser.Setup(x => x.Parse(It.IsAny<Stream>()))
            .Returns((mockDataSet, null));
        _mockDataSetConverter.Setup(x => x.ConvertForDatabase(mockDataSet))
            .Returns(mockDataSet);

        // Setup other required mocks
        _mockProcessingLogRepository2.Setup(x => x.GetDeltaLinkForSyncAsync(_testSiteId, _testListId))
            .ReturnsAsync("test-delta-link");
        _mockGraph.Setup(x => x.PullItemsDeltaAsync(_testSiteId, _testListId, "test-delta-link"))
            .ReturnsAsync(("new-delta-link", new List<string> { itemId }));

        _mockProcessedFileRepository.Setup(x => x.GetByIdAsync(It.IsAny<int>()))
            .ReturnsAsync(new ProcessedFile());

        // Act
        var result = await handler.FetchAndStoreDeltaAsync();

        // Assert
        result.Success.Should().BeTrue();

        // Verify that duplicate check was called with the correct hash and size
        _mockProcessedFileRepository.Verify(x => x.ExistsByHashAsync(It.IsAny<string>(), expectedSize), Times.Once);
    }

    private void VerifyLogMessage(string expectedMessage, Times times)
    {
        _mockLogger.Verify(
            l => l.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);
    }

    public void Dispose()
    {
        _mockContext.Dispose();
    }
}