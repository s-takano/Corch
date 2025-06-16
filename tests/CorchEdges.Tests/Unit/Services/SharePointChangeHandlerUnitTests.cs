using System.Data.Common;
using Microsoft.Extensions.Logging;
using Moq;
using System.Data;
using Xunit;
using FluentAssertions;
using CorchEdges.Abstractions;
using CorchEdges.Data;
using CorchEdges.Data.Abstractions;
using CorchEdges.Data.Entities;
using CorchEdges.Models;
using CorchEdges.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Graph.Models;

namespace CorchEdges.Tests.Unit.Services
{
    public class SharePointChangeHandlerUnitTests : IDisposable
    {
        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<IGraphFacade> _mockGraph;
        private readonly Mock<IExcelParser> _mockParser;
        private readonly Mock<IDatabaseWriter> _mockDb;
        private readonly EdgesDbContext _mockContext;
        private readonly string _testSiteId = "12345678-1234-1234-1234-123456789012";
        private readonly string _testListId = "87654321-4321-4321-4321-210987654321";

        public SharePointChangeHandlerUnitTests()
        {
            _mockLogger = new Mock<ILogger>();
            _mockGraph = new Mock<IGraphFacade>();
            _mockParser = new Mock<IExcelParser>();
            _mockDb = new Mock<IDatabaseWriter>();
            _mockContext = CreateInMemoryDbContext();
        }

        private EdgesDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<EdgesDbContext>()
                .UseSqlite("DataSource=:memory:")
                .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.AmbientTransactionWarning))
                .Options;

            var context = new EdgesDbContext(options);
            context.Database.OpenConnection();
            context.Database.EnsureCreated();
            return context;
        }


        private SharePointChangeHandler CreateHandler(string watchedPath = "/sites/test/Shared Documents/WatchedFolder")
        {
            return new SharePointChangeHandler(
                _mockLogger.Object,
                _mockGraph.Object,
                _mockParser.Object,
                _mockDb.Object,
                _mockContext,
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
                new SharePointChangeHandler(
                    null!,
                    _mockGraph.Object,
                    _mockParser.Object,
                    _mockDb.Object,
                    _mockContext,
                    _testSiteId,
                    _testListId,
                    "/test/path"));
        }

        [Fact]
        public void Constructor_WithNullGraph_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new SharePointChangeHandler(
                    _mockLogger.Object,
                    null!,
                    _mockParser.Object,
                    _mockDb.Object,
                    _mockContext,
                    _testSiteId,
                    _testListId,
                    "/test/path"));
        }

        [Fact]
        public void Constructor_WithNullParser_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new SharePointChangeHandler(
                    _mockLogger.Object,
                    _mockGraph.Object,
                    null!,
                    _mockDb.Object,
                    _mockContext,
                    _testSiteId,
                    _testListId,
                    "/test/path"));
        }

        [Fact]
        public void Constructor_WithNullDatabaseWriter_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new SharePointChangeHandler(
                    _mockLogger.Object,
                    _mockGraph.Object,
                    _mockParser.Object,
                    null!,
                    _mockContext,
                    _testSiteId,
                    _testListId,
                    "/test/path"));
        }

        [Fact]
        public void Constructor_WithNullContext_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new SharePointChangeHandler(
                    _mockLogger.Object,
                    _mockGraph.Object,
                    _mockParser.Object,
                    _mockDb.Object,
                    null!,
                    _testSiteId,
                    _testListId,
                    "/test/path"));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("   ")]
        public void Constructor_WithInvalidSiteId_ShouldThrowArgumentException(string invalidSiteId)
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                new SharePointChangeHandler(
                    _mockLogger.Object,
                    _mockGraph.Object,
                    _mockParser.Object,
                    _mockDb.Object,
                    _mockContext,
                    invalidSiteId,
                    _testListId,
                    "/test/path"));

            exception.ParamName.Should().Be("siteId");
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("   ")]
        public void Constructor_WithInvalidListId_ShouldThrowArgumentException(string invalidListId)
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                new SharePointChangeHandler(
                    _mockLogger.Object,
                    _mockGraph.Object,
                    _mockParser.Object,
                    _mockDb.Object,
                    _mockContext,
                    _testSiteId,
                    invalidListId,
                    "/test/path"));

            exception.ParamName.Should().Be("listId");
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("   ")]
        public void Constructor_WithInvalidWatchedPath_ShouldThrowArgumentException(string invalidWatchedPath)
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                new SharePointChangeHandler(
                    _mockLogger.Object,
                    _mockGraph.Object,
                    _mockParser.Object,
                    _mockDb.Object,
                    _mockContext,
                    _testSiteId,
                    _testListId,
                    invalidWatchedPath));

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
                new SharePointChangeHandler(
                    _mockLogger.Object,
                    _mockGraph.Object,
                    _mockParser.Object,
                    _mockDb.Object,
                    _mockContext,
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
                new SharePointChangeHandler(
                    _mockLogger.Object,
                    _mockGraph.Object,
                    _mockParser.Object,
                    _mockDb.Object,
                    _mockContext,
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
            var handler = new SharePointChangeHandler(
                _mockLogger.Object,
                _mockGraph.Object,
                _mockParser.Object,
                _mockDb.Object,
                _mockContext,
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

            _mockParser.Setup(p => p.Parse(It.IsAny<byte[]>()))
                .Returns((new DataSet(), string.Empty));

            _mockGraph.Setup(g => g.DownloadAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new MemoryStream());

            // Act
            await handler.HandleAsync([changeNotification]);

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

            // Act
            await handler.HandleAsync([changeNotification]);

            // Assert
            VerifyLogMessage("Skipping item outside watched folder", Times.Once());
            _mockParser.Verify(p => p.Parse(It.IsAny<byte[]>()), Times.Never);
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

            if (shouldProcess)
            {
                _mockParser.Setup(p => p.Parse(It.IsAny<byte[]>()))
                    .Returns((new DataSet(), string.Empty));
                _mockGraph.Setup(g => g.DownloadAsync(It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(new MemoryStream());
            }

            // Act
            await handler.HandleAsync([changeNotification]);

            // Assert
            if (shouldProcess)
            {
                VerifyLogMessage("Skipping item outside watched folder", Times.Never());
            }
            else
            {
                VerifyLogMessage("Skipping item outside watched folder", Times.Once());
                _mockParser.Verify(p => p.Parse(It.IsAny<byte[]>()), Times.Never);
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

            _mockParser.Setup(p => p.Parse(It.IsAny<byte[]>()))
                .Returns((new DataSet(), string.Empty));
            _mockGraph.Setup(g => g.DownloadAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new MemoryStream());

            // Act
            await handler.HandleAsync([changeNotification]);

            // Assert
            VerifyLogMessage("Skipping item outside watched folder", Times.Never());
        }

        [Fact]
        public async Task EnsureGraphConnectionAsync_ShouldReturnGraphConnectionResult()
        {
            // Arrange
            var handler = CreateHandler();
            var connectionResult = new ConnectionTestResult(true, null, null);
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
            var handler = CreateHandler();
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

            _mockParser.Setup(p => p.Parse(It.IsAny<byte[]>()))
                .Returns((new DataSet(), string.Empty));

            _mockGraph.Setup(g => g.DownloadAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new MemoryStream());

            // Act
            await handler.HandleAsync([changeNotification]);

            // Assert
            var processingLogs = await _mockContext.ProcessingLogs.ToListAsync();

            processingLogs.Should().HaveCount(1);
            var log = processingLogs.First();

            log.SiteId.Should().Be(testSiteId);
            log.ListId.Should().Be(testListId);
            log.Status.Should().Be("Success");
            log.LastProcessedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
            log.SuccessfulItems.Should().Be(1);
            log.FailedItems.Should().Be(0);
            log.LastProcessedCount.Should().Be(1);
        }

        [Fact]
        public async Task HandleAsync_WhenProcessingFails_ShouldStoreProcessingLogWithError()
        {
            // Arrange
            var handler = CreateHandler();
            var testException = new InvalidOperationException("Test parsing error");

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

            _mockParser.Setup(p => p.Parse(It.IsAny<byte[]>()))
                .Returns((null, "Test parsing error"));

            // Act
            await handler.HandleAsync([changeNotification]);

            // Assert
            var processingLogs = await _mockContext.ProcessingLogs.ToListAsync();

            processingLogs.Should().HaveCount(1);
            var log = processingLogs.First();

            log.SiteId.Should().Be(_testSiteId);
            log.ListId.Should().Be(_testListId);
            log.Status.Should().Be("Failed");
            log.LastError.Should().Be("Test parsing error");
            log.FailedItems.Should().Be(1);
            log.SuccessfulItems.Should().Be(0);
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
}