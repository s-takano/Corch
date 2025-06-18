using CorchEdges.Data;
using CorchEdges.Data.Entities;
using CorchEdges.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CorchEdges.Tests.Unit.Data.Repositories;

/// <summary>
/// Unit tests for ProcessingLogRepository.
/// </summary>
public class ProcessingLogRepositoryTests : IDisposable
{
    private readonly EdgesDbContext _context;
    private readonly ProcessingLogRepository _repository;

    public ProcessingLogRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<EdgesDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new EdgesDbContext(options);
        _repository = new ProcessingLogRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region GetDeltaLinkForSyncAsync Tests

    [Fact]
    public async Task GetDeltaLinkForSyncAsync_WhenNoPreviousLogs_ReturnsLatest()
    {
        // Arrange
        const string siteId = "site123";
        const string listId = "list456";

        // Act
        var result = await _repository.GetDeltaLinkForSyncAsync(siteId, listId);

        // Assert
        Assert.Equal("latest", result);
    }

    [Fact]
    public async Task GetDeltaLinkForSyncAsync_WhenLogExistsButNoDeltaLink_ReturnsLatest()
    {
        // Arrange
        const string siteId = "site123";
        const string listId = "list456";

        var existingLog = new ProcessingLog
        {
            SiteId = siteId,
            ListId = listId,
            DeltaLink = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Status = ProcessingStatus.Completed
        };

        _context.ProcessingLogs.Add(existingLog);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetDeltaLinkForSyncAsync(siteId, listId);

        // Assert
        Assert.Equal("latest", result);
    }

    [Fact]
    public async Task GetDeltaLinkForSyncAsync_WhenLogExistsWithDeltaLink_ReturnsDeltaLink()
    {
        // Arrange
        const string siteId = "site123";
        const string listId = "list456";
        const string expectedDeltaLink = "https://sharepoint.com/delta/12345";

        var existingLog = new ProcessingLog
        {
            SiteId = siteId,
            ListId = listId,
            DeltaLink = expectedDeltaLink,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Status = ProcessingStatus.Completed
        };

        _context.ProcessingLogs.Add(existingLog);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetDeltaLinkForSyncAsync(siteId, listId);

        // Assert
        Assert.Equal(expectedDeltaLink, result);
    }

    [Fact]
    public async Task GetDeltaLinkForSyncAsync_WhenMultipleLogsExist_ReturnsLatestDeltaLink()
    {
        // Arrange
        const string siteId = "site123";
        const string listId = "list456";
        const string latestDeltaLink = "https://sharepoint.com/delta/latest";
        const string oldDeltaLink = "https://sharepoint.com/delta/old";

        var oldLog = new ProcessingLog
        {
            SiteId = siteId,
            ListId = listId,
            DeltaLink = oldDeltaLink,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            UpdatedAt = DateTime.UtcNow.AddHours(-2),
            Status = ProcessingStatus.Completed
        };

        var latestLog = new ProcessingLog
        {
            SiteId = siteId,
            ListId = listId,
            DeltaLink = latestDeltaLink,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Status = ProcessingStatus.Completed
        };

        _context.ProcessingLogs.AddRange(oldLog, latestLog);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetDeltaLinkForSyncAsync(siteId, listId);

        // Assert
        Assert.Equal(latestDeltaLink, result);
    }

    [Fact]
    public async Task GetDeltaLinkForSyncAsync_WhenDifferentSiteAndList_ReturnsLatest()
    {
        // Arrange
        const string siteId = "site123";
        const string listId = "list456";
        const string differentSiteId = "differentSite";
        const string differentListId = "differentList";

        var differentLog = new ProcessingLog
        {
            SiteId = differentSiteId,
            ListId = differentListId,
            DeltaLink = "https://sharepoint.com/delta/other",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Status = ProcessingStatus.Completed
        };

        _context.ProcessingLogs.Add(differentLog);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetDeltaLinkForSyncAsync(siteId, listId);

        // Assert
        Assert.Equal("latest", result);
    }

    #endregion

    #region RecordSuccessfulSyncAsync Tests

    [Fact]
    public async Task RecordSuccessfulSyncAsync_WhenNoExistingLog_CreatesNewLog()
    {
        // Arrange
        const string siteId = "site123";
        const string listId = "list456";
        const string deltaLink = "https://sharepoint.com/delta/12345";
        const int processedCount = 5;
        const string subscriptionId = "sub789";

        // Act
        var result = await _repository.RecordSuccessfulSyncAsync(siteId, listId, deltaLink, processedCount, subscriptionId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(siteId, result.SiteId);
        Assert.Equal(listId, result.ListId);
        Assert.Equal(deltaLink, result.DeltaLink);
        Assert.Equal(processedCount, result.LastProcessedCount);
        Assert.Equal(ProcessingStatus.Completed, result.Status);
        Assert.Equal(processedCount, result.SuccessfulItems);
        Assert.Equal(subscriptionId, result.SubscriptionId);
        Assert.Null(result.LastError);
        Assert.True(result.Id > 0);
        Assert.True((DateTime.UtcNow - result.CreatedAt).TotalSeconds < 5);
        Assert.True((DateTime.UtcNow - result.UpdatedAt).TotalSeconds < 5);
        Assert.True((DateTime.UtcNow - result.LastProcessedAt).TotalSeconds < 5);
    }

    [Fact]
    public async Task RecordSuccessfulSyncAsync_WhenNoSubscriptionId_CreatesNewLogWithoutSubscriptionId()
    {
        // Arrange
        const string siteId = "site123";
        const string listId = "list456";
        const string deltaLink = "https://sharepoint.com/delta/12345";
        const int processedCount = 3;

        // Act
        var result = await _repository.RecordSuccessfulSyncAsync(siteId, listId, deltaLink, processedCount);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(siteId, result.SiteId);
        Assert.Equal(listId, result.ListId);
        Assert.Equal(deltaLink, result.DeltaLink);
        Assert.Equal(processedCount, result.LastProcessedCount);
        Assert.Equal(ProcessingStatus.Completed, result.Status);
        Assert.Equal(processedCount, result.SuccessfulItems);
        Assert.Null(result.SubscriptionId);
        Assert.Null(result.LastError);
    }


    #endregion

    #region RecordFailedSyncAsync Tests

    [Fact]
    public async Task RecordFailedSyncAsync_WhenNoExistingLog_CreatesNewFailedLog()
    {
        // Arrange
        const string siteId = "site123";
        const string listId = "list456";
        const string errorMessage = "Connection timeout";
        const string subscriptionId = "sub789";

        // Act
        var result = await _repository.RecordFailedSyncAsync(siteId, listId, errorMessage, 1, subscriptionId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(siteId, result.SiteId);
        Assert.Equal(listId, result.ListId);
        Assert.Equal(ProcessingStatus.Failed, result.Status);
        Assert.Equal(errorMessage, result.LastError);
        Assert.Equal(1, result.FailedItems);
        Assert.Equal(subscriptionId, result.SubscriptionId);
        Assert.True(result.Id > 0);
        Assert.True((DateTime.UtcNow - result.CreatedAt).TotalSeconds < 5);
        Assert.True((DateTime.UtcNow - result.UpdatedAt).TotalSeconds < 5);
    }

    [Fact]
    public async Task RecordFailedSyncAsync_WhenNoSubscriptionId_CreatesNewFailedLogWithoutSubscriptionId()
    {
        // Arrange
        const string siteId = "site123";
        const string listId = "list456";
        const string errorMessage = "Authentication failed";

        // Act
        var result = await _repository.RecordFailedSyncAsync(siteId, listId, errorMessage, 1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(siteId, result.SiteId);
        Assert.Equal(listId, result.ListId);
        Assert.Equal(ProcessingStatus.Failed, result.Status);
        Assert.Equal(errorMessage, result.LastError);
        Assert.Equal(1, result.FailedItems);
        Assert.Null(result.SubscriptionId);
    }


    #endregion

    #region GetRecentProcessingHistoryAsync Tests

    [Fact]
    public async Task GetRecentProcessingHistoryAsync_WhenNoLogs_ReturnsEmptyList()
    {
        // Arrange
        const string siteId = "site123";
        const string listId = "list456";

        // Act
        var result = await _repository.GetRecentProcessingHistoryAsync(siteId, listId);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetRecentProcessingHistoryAsync_WhenLogsExist_ReturnsLogsInDescendingOrder()
    {
        // Arrange
        const string siteId = "site123";
        const string listId = "list456";

        var log1 = new ProcessingLog
        {
            SiteId = siteId,
            ListId = listId,
            CreatedAt = DateTime.UtcNow.AddHours(-3),
            UpdatedAt = DateTime.UtcNow.AddHours(-3),
            Status = ProcessingStatus.Completed
        };

        var log2 = new ProcessingLog
        {
            SiteId = siteId,
            ListId = listId,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            UpdatedAt = DateTime.UtcNow.AddHours(-1),
            Status = ProcessingStatus.Failed
        };

        var log3 = new ProcessingLog
        {
            SiteId = siteId,
            ListId = listId,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            UpdatedAt = DateTime.UtcNow.AddHours(-2),
            Status = ProcessingStatus.Processing
        };

        _context.ProcessingLogs.AddRange(log1, log2, log3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetRecentProcessingHistoryAsync(siteId, listId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal(log2.Id, result[0].Id); // Most recent first
        Assert.Equal(log3.Id, result[1].Id);
        Assert.Equal(log1.Id, result[2].Id); // Oldest last
    }

    [Fact]
    public async Task GetRecentProcessingHistoryAsync_WhenMoreLogsThanCount_ReturnsLimitedResults()
    {
        // Arrange
        const string siteId = "site123";
        const string listId = "list456";
        const int requestedCount = 2;

        var logs = new List<ProcessingLog>();
        for (int i = 0; i < 5; i++)
        {
            logs.Add(new ProcessingLog
            {
                SiteId = siteId,
                ListId = listId,
                CreatedAt = DateTime.UtcNow.AddHours(-i),
                UpdatedAt = DateTime.UtcNow.AddHours(-i),
                Status = ProcessingStatus.Completed
            });
        }

        _context.ProcessingLogs.AddRange(logs);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetRecentProcessingHistoryAsync(siteId, listId, requestedCount);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(requestedCount, result.Count);
        Assert.Equal(logs[0].Id, result[0].Id); // Most recent
        Assert.Equal(logs[1].Id, result[1].Id);
    }

    [Fact]
    public async Task GetRecentProcessingHistoryAsync_WhenDifferentSiteAndList_ReturnsEmptyList()
    {
        // Arrange
        const string siteId = "site123";
        const string listId = "list456";
        const string differentSiteId = "differentSite";
        const string differentListId = "differentList";

        var differentLog = new ProcessingLog
        {
            SiteId = differentSiteId,
            ListId = differentListId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Status = ProcessingStatus.Completed
        };

        _context.ProcessingLogs.Add(differentLog);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetRecentProcessingHistoryAsync(siteId, listId);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetRecentProcessingHistoryAsync_WithDefaultCount_Returns10Records()
    {
        // Arrange
        const string siteId = "site123";
        const string listId = "list456";

        var logs = new List<ProcessingLog>();
        for (int i = 0; i < 15; i++)
        {
            logs.Add(new ProcessingLog
            {
                SiteId = siteId,
                ListId = listId,
                CreatedAt = DateTime.UtcNow.AddHours(-i),
                UpdatedAt = DateTime.UtcNow.AddHours(-i),
                Status = ProcessingStatus.Completed
            });
        }

        _context.ProcessingLogs.AddRange(logs);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetRecentProcessingHistoryAsync(siteId, listId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(10, result.Count); // Default count
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task IntegrationTest_CompleteWorkflow_WorksCorrectly()
    {
        // Arrange
        const string siteId = "site123";
        const string listId = "list456";
        const string deltaLink1 = "https://sharepoint.com/delta/1";
        const string deltaLink2 = "https://sharepoint.com/delta/2";
        const string errorMessage = "Test error";

        // Act & Assert

        // 1. Initially no delta link exists
        var initialDeltaLink = await _repository.GetDeltaLinkForSyncAsync(siteId, listId);
        Assert.Equal("latest", initialDeltaLink);

        // 2. Record first successful sync
        var firstSync = await _repository.RecordSuccessfulSyncAsync(siteId, listId, deltaLink1, 5);
        Assert.Equal(ProcessingStatus.Completed, firstSync.Status);
        Assert.Equal(5, firstSync.SuccessfulItems);

        // 3. Get delta link should return the first one
        var deltaAfterFirst = await _repository.GetDeltaLinkForSyncAsync(siteId, listId);
        Assert.Equal(deltaLink1, deltaAfterFirst);

        // 4. Record a failed sync
        var failedSync = await _repository.RecordFailedSyncAsync(siteId, listId, errorMessage, 1);
        Assert.Equal(ProcessingStatus.Failed, failedSync.Status);
        Assert.Equal(1, failedSync.FailedItems);

        // 5. Record second successful sync
        var secondSync = await _repository.RecordSuccessfulSyncAsync(siteId, listId, deltaLink2, 3);
        Assert.Equal(ProcessingStatus.Completed, secondSync.Status);
        Assert.Equal(3, secondSync.SuccessfulItems); 

        // 6. Get delta link should return the second one
        var deltaAfterSecond = await _repository.GetDeltaLinkForSyncAsync(siteId, listId);
        Assert.Equal(deltaLink2, deltaAfterSecond);

        // 7. Get recent history should show the progression
        var history = await _repository.GetRecentProcessingHistoryAsync(siteId, listId);
        Assert.Equal(3, history.Count); // Only one record since we keep updating the same one
        Assert.Equal(ProcessingStatus.Completed, history[0].Status);
        Assert.Equal(3, history[0].SuccessfulItems);
        Assert.Equal(0, history[0].FailedItems);
        Assert.Equal(0, history[1].SuccessfulItems);
        Assert.Equal(1, history[1].FailedItems);
        Assert.Equal(5, history[2].SuccessfulItems);
        Assert.Equal(0, history[2].FailedItems);
    }

    #endregion
}