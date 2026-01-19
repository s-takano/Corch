using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Messaging.ServiceBus;
using CorchEdges.Abstractions;
using CorchEdges.Data;
using CorchEdges.Functions.SharePoint;
using CorchEdges.Models;
using CorchEdges.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;

namespace CorchEdges.Tests.Unit.Functions;

[Trait("Category", TestCategories.Unit)]
public class SharePointChangeNotificationProcessorTests : IDisposable
{
    private readonly Mock<ILogger<SharePointChangeNotificationProcessor>> _mockLogger;
    private readonly Mock<ISharePointSyncProcessor> _mockProcessor;
    private readonly Mock<BlobServiceClient> _mockBlobServiceClient;
    private readonly Mock<BlobContainerClient> _mockBlobContainerClient;
    private readonly Mock<ServiceBusClient> _mockServiceBusClient;
    private readonly Mock<ServiceBusSender> _mockServiceBusSender;
    private readonly SharePointChangeNotificationProcessor _function;

    private EdgesDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<EdgesDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // Unique DB per test
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new EdgesDbContext(options);
    }

    public SharePointChangeNotificationProcessorTests()
    {
        _mockLogger = new Mock<ILogger<SharePointChangeNotificationProcessor>>();
        _mockProcessor = new Mock<ISharePointSyncProcessor>();
        _mockBlobServiceClient = new Mock<BlobServiceClient>();
        _mockBlobContainerClient = new Mock<BlobContainerClient>();
        _mockServiceBusClient = new Mock<ServiceBusClient>();
        _mockServiceBusSender = new Mock<ServiceBusSender>();

        // Setup blob service client to return container client
        _mockBlobServiceClient
            .Setup(x => x.GetBlobContainerClient("failed-changes"))
            .Returns(_mockBlobContainerClient.Object);

        // Setup container creation to not throw
        _mockBlobContainerClient
            .Setup(x => x.CreateIfNotExistsAsync(It.IsAny<PublicAccessType>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Mock.Of<Response<BlobContainerInfo>>()));

        // Setup Service Bus sender
        _mockServiceBusClient
            .Setup(x => x.CreateSender("sp-changes"))
            .Returns(_mockServiceBusSender.Object);

        _function = new SharePointChangeNotificationProcessor(
            _mockLogger.Object,
            _mockProcessor.Object,
            _mockBlobServiceClient.Object,
            _mockServiceBusClient.Object);
    }


    [Fact]
    public async Task RunAsync_WithValidGraphConnection_ShouldProcessNotificationSuccessfully()
    {
        // Arrange
        var testMessage = JsonSerializer.Serialize(new NotificationEnvelope
        {
            Value = new[]
            {
                new SharePointNotification
                {
                    Resource = "sites/test-site/lists/test-list/items/123",
                    ChangeType = "Updated"
                }
            }
        });

        _mockProcessor.Setup(x => x.EnsureGraphConnectionAsync())
            .ReturnsAsync(true);

        _mockProcessor.Setup(x => x.FetchAndStoreDeltaAsync(It.IsAny<int>()))
            .ReturnsAsync(SharePointSyncResult.Succeeded());

        _mockProcessor.Setup(x => x.SuccessfulItems)
            .Returns(1);

        // Act
        var result = await _function.ProcessNotificationAsync(testMessage);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        
        _mockProcessor.Verify(x => x.EnsureGraphConnectionAsync(), Times.Once);
        _mockProcessor.Verify(x => x.FetchAndStoreDeltaAsync(It.IsAny<int>()), Times.Once);
        
        VerifyLogMessage("Verifying Graph API connection...", Times.Once());
        VerifyLogMessage("Deserializing SharePoint change notification...", Times.Once());
        VerifyLogMessage("Successfully processed batch", Times.Once());
    }

    [Fact]
    public async Task RunAsync_WithFailedGraphConnection_ShouldSaveMessageAndReturnFailed()
    {
        // Arrange
        var testMessage = JsonSerializer.Serialize(new NotificationEnvelope
        {
            Value = new[]
            {
                new SharePointNotification
                {
                    Resource = "sites/test-site/lists/test-list/items/123",
                    ChangeType = "Updated"
                }
            }
        });

        _mockProcessor.Setup(x => x.EnsureGraphConnectionAsync())
            .ReturnsAsync(false);

        _mockBlobContainerClient.Setup(x => x.UploadBlobAsync(
                It.IsAny<string>(),
                It.IsAny<BinaryData>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Mock.Of<Response<BlobContentInfo>>()));

        // Act
        var result = await _function.ProcessNotificationAsync(testMessage);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorReason.Should().Be("Can't connect to Graph API");
        
        _mockProcessor.Verify(x => x.EnsureGraphConnectionAsync(), Times.Once);
        _mockProcessor.Verify(x => x.FetchAndStoreDeltaAsync(int.MaxValue), Times.Never);
        
        _mockBlobContainerClient.Verify(x => x.UploadBlobAsync(
            It.Is<string>(s => s.StartsWith("graph-connection-failed-")),
            It.IsAny<BinaryData>(),
            It.IsAny<CancellationToken>()), Times.Once);
        
        VerifyLogMessage("Graph API connection failed - aborting message processing", Times.Once());
        VerifyLogMessage("Message saved to", Times.Once());
    }
    
    [Fact]
    public async Task RunAsync_WithFetchAndStoreDeltaFailure_ShouldSaveMessageAndBreakProcessing()
    {
        // Arrange
        var testMessage = JsonSerializer.Serialize(new NotificationEnvelope
        {
            Value = new[]
            {
                new SharePointNotification
                {
                    Resource = "sites/test-site/lists/test-list/items/123",
                    ChangeType = "Updated"
                },
                new SharePointNotification
                {
                    Resource = "sites/test-site/lists/test-list/items/456", 
                    ChangeType = "Updated"
                }
            }
        });

        var failureReason = "Database connection failed";

        _mockProcessor.Setup(x => x.EnsureGraphConnectionAsync())
            .ReturnsAsync(true);

        _mockProcessor.Setup(x => x.FetchAndStoreDeltaAsync(It.IsAny<int>()))
            .ReturnsAsync(SharePointSyncResult.Failed(failureReason));

        _mockBlobContainerClient.Setup(x => x.UploadBlobAsync(
                It.IsAny<string>(),
                It.IsAny<BinaryData>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Mock.Of<Response<BlobContentInfo>>()));

        // Act
        var result = await _function.ProcessNotificationAsync(testMessage);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue(); // Function completes successfully even though processing failed
    
        _mockProcessor.Verify(x => x.EnsureGraphConnectionAsync(), Times.Once);
        _mockProcessor.Verify(x => x.FetchAndStoreDeltaAsync(It.IsAny<int>()), Times.Once); // Should only be called once, then break
    
        _mockBlobContainerClient.Verify(x => x.UploadBlobAsync(
            It.Is<string>(s => s.StartsWith("processing-error-")),
            It.IsAny<BinaryData>(),
            It.IsAny<CancellationToken>()), Times.Once);
    
        VerifyLogMessage("Processing 2 change notifications", Times.Once());
        VerifyLogMessage($"Error processing change notification: {failureReason}", Times.Once());
    }
    

    [Fact]
    public async Task RunAsync_WithInvalidJson_ShouldThrowAndSaveMessage()
    {
        // Arrange
        var invalidMessage = "{ invalid json }";

        _mockBlobContainerClient.Setup(x => x.UploadBlobAsync(
                It.IsAny<string>(),
                It.IsAny<BinaryData>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Mock.Of<Response<BlobContentInfo>>()));

        _mockProcessor.Setup(x => x.EnsureGraphConnectionAsync())
            .ReturnsAsync(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<JsonException>(() => _function.ProcessNotificationAsync(invalidMessage));
        
        _mockBlobContainerClient.Verify(x => x.UploadBlobAsync(
            It.Is<string>(s => s.StartsWith("processing-error-")),
            It.IsAny<BinaryData>(),
            It.IsAny<CancellationToken>()), Times.Once);
        
        VerifyLogMessage("Unhandled error during message processing - saved to", Times.Once());
    }

    [Fact]
    public async Task RunAsync_WithContinuationPayload_ShouldFetchItemsAndNotSendContinuation()
    {
        // Arrange
        var continuationPayload = JsonSerializer.Serialize(new
        {
            ItemIds = new[] { "1", "2" },
            DeltaLink = "https://example.com/deltaLink"
        });

        _mockProcessor.Setup(x => x.EnsureGraphConnectionAsync())
            .ReturnsAsync(true);

        _mockProcessor.Setup(x => x.FetchAndStoreItemsAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<int>()))
            .ReturnsAsync(SharePointSyncResult.Succeeded());

        _mockServiceBusSender
            .Setup(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _function.ProcessNotificationAsync(continuationPayload);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        _mockProcessor.Verify(x => x.FetchAndStoreItemsAsync(
            It.Is<IReadOnlyList<string>>(ids => ids.SequenceEqual(new[] { "1", "2" })),
            It.Is<string>(link => link == "https://example.com/deltaLink"),
            It.Is<bool>(finalize => finalize == true),
            It.Is<int>(batch => batch == 200)), Times.Once);

        _mockServiceBusSender.Verify(
            x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_WithContinuationPayloadAndMoreWork_ShouldSendContinuationMessage()
    {
        // Arrange
        var continuationPayload = JsonSerializer.Serialize(new
        {
            ItemIds = new[] { "1", "2" },
            DeltaLink = "https://example.com/deltaLink"
        });

        _mockProcessor.Setup(x => x.EnsureGraphConnectionAsync())
            .ReturnsAsync(true);

        _mockProcessor.Setup(x => x.FetchAndStoreItemsAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<int>()))
            .ReturnsAsync(new SharePointSyncResult(true)
            {
                Success = true,
                PendingDeltaLink = "https://example.com/nextDelta",
                RemainingItemIds = new List<string> { "3", "4" }
            });

        _mockServiceBusSender
            .Setup(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _function.ProcessNotificationAsync(continuationPayload);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        _mockServiceBusSender.Verify(
            x => x.SendMessageAsync(
                It.Is<ServiceBusMessage>(m =>
                    m.Body.ToString().Contains("https://example.com/nextDelta") &&
                    m.Body.ToString().Contains("\"3\"") &&
                    m.Body.ToString().Contains("\"4\"")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_WithHandlerException_ShouldThrowAndSaveMessage()
    {
        // Arrange
        var testMessage = JsonSerializer.Serialize(new NotificationEnvelope
        {
            Value = new[]
            {
                new SharePointNotification
                {
                    Resource = "sites/test-site/lists/test-list/items/123",
                    ChangeType = "Updated"
                }
            }
        });

        _mockProcessor.Setup(x => x.EnsureGraphConnectionAsync())
            .ReturnsAsync(true);

        _mockProcessor.Setup(x => x.FetchAndStoreDeltaAsync(It.IsAny<int>()))
            .ThrowsAsync(new InvalidOperationException("Handler error"));

        _mockBlobContainerClient.Setup(x => x.UploadBlobAsync(
                It.IsAny<string>(),
                It.IsAny<BinaryData>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Mock.Of<Response<BlobContentInfo>>()));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _function.ProcessNotificationAsync(testMessage));
        
        exception.Message.Should().Be("Handler error");
        
        _mockBlobContainerClient.Verify(x => x.UploadBlobAsync(
            It.Is<string>(s => s.StartsWith("processing-error-")),
            It.IsAny<BinaryData>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_WithEmptyNotificationArray_ShouldSucceed()
    {
        // Arrange
        var testMessage = JsonSerializer.Serialize(new NotificationEnvelope
        {
            Value = Array.Empty<SharePointNotification>()
        });

        _mockProcessor.Setup(x => x.EnsureGraphConnectionAsync())
            .ReturnsAsync(true);

        _mockProcessor.Setup(x => x.SuccessfulItems)
            .Returns(0);

        // Act
        var result = await _function.ProcessNotificationAsync(testMessage);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        
        _mockProcessor.Verify(x => x.FetchAndStoreDeltaAsync(int.MaxValue), Times.Never);
        
        VerifyLogMessage("Processing 0 change notifications", Times.Once());
    }

    [Fact]
    public async Task RunAsync_WithNullNotificationEnvelope_ShouldCreateEmptyEnvelope()
    {
        // Arrange
        var testMessage = "null";

        _mockProcessor.Setup(x => x.EnsureGraphConnectionAsync())
            .ReturnsAsync(true);

        _mockProcessor.Setup(x => x.SuccessfulItems)
            .Returns(0);

        // Act
        var result = await _function.ProcessNotificationAsync(testMessage);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        
        _mockProcessor.Verify(x => x.FetchAndStoreDeltaAsync(It.IsAny<int>()), Times.Never);
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
        // Clean up any resources if needed
    }
}
