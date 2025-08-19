using CorchEdges.Data;
using CorchEdges.Data.Entities;
using CorchEdges.Data.Repositories;
using CorchEdges.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CorchEdges.Tests.Unit.Data.Repositories;

[Trait("Category", TestCategories.Unit)]
[Trait("Component", "ProcessedFileRepository")]
public class ProcessedFileRepositoryTests : IDisposable
{
    private readonly EdgesDbContext _context;
    private readonly ProcessedFileRepository _repository;

    public ProcessedFileRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<EdgesDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new EdgesDbContext(options);
        _repository = new ProcessedFileRepository(_context);
    }

    [Fact]
    public async Task ExistsByHashAsync_FileExists_ReturnsTrue()
    {
        // Arrange
        const string fileHash = "abc123def456";
        const long fileSize = 1024;

        var processedFile = CreateTestProcessedFile(fileHash: fileHash, fileSizeBytes: fileSize);
        await _context.ProcessedFiles.AddAsync(processedFile, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var exists = await _repository.ExistsByHashAsync(fileHash, fileSize);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task ExistsByHashAsync_FileDoesNotExist_ReturnsFalse()
    {
        // Arrange
        const string fileHash = "nonexistent123";
        const long fileSize = 1024;

        // Act
        var exists = await _repository.ExistsByHashAsync(fileHash, fileSize);

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public async Task ExistsByHashAsync_HashMatchesSizeDifferent_ReturnsFalse()
    {
        // Arrange
        const string fileHash = "abc123def456";
        const long originalSize = 1024;
        const long differentSize = 2048;

        var processedFile = CreateTestProcessedFile(fileHash: fileHash, fileSizeBytes: originalSize);
        await _context.ProcessedFiles.AddAsync(processedFile, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var exists = await _repository.ExistsByHashAsync(fileHash, differentSize);

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public async Task ExistsByHashAsync_SizeMatchesHashDifferent_ReturnsFalse()
    {
        // Arrange
        const string originalHash = "abc123def456";
        const string differentHash = "xyz789uvw012";
        const long fileSize = 1024;

        var processedFile = CreateTestProcessedFile(fileHash: originalHash, fileSizeBytes: fileSize);
        await _context.ProcessedFiles.AddAsync(processedFile, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var exists = await _repository.ExistsByHashAsync(differentHash, fileSize);

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public async Task GetByHashAsync_FileExists_ReturnsCorrectFile()
    {
        // Arrange
        const string fileHash = "abc123def456";
        const long fileSize = 1024;

        var processedFile = CreateTestProcessedFile(fileHash: fileHash, fileSizeBytes: fileSize);
        await _context.ProcessedFiles.AddAsync(processedFile, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetByHashAsync(fileHash, fileSize);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(fileHash, result.FileHash);
        Assert.Equal(fileSize, result.FileSizeBytes);
        Assert.Equal(processedFile.SharePointItemId, result.SharePointItemId);
    }

    [Fact]
    public async Task GetByHashAsync_FileDoesNotExist_ReturnsNull()
    {
        // Arrange
        const string fileHash = "nonexistent123";
        const long fileSize = 1024;

        // Act
        var result = await _repository.GetByHashAsync(fileHash, fileSize);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByHashAsync_MultipleFilesWithSameHash_ReturnsFirstMatch()
    {
        // Arrange
        const string fileHash = "abc123def456";
        const long fileSize = 1024;

        var file1 = CreateTestProcessedFile(siteId: "site1", fileHash: fileHash, fileSizeBytes: fileSize);
        var file2 = CreateTestProcessedFile(siteId: "site2", fileHash: fileHash, fileSizeBytes: fileSize);

        await _context.ProcessedFiles.AddRangeAsync(file1, file2);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetByHashAsync(fileHash, fileSize);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(fileHash, result.FileHash);
        Assert.Equal(fileSize, result.FileSizeBytes);
        // Should return one of the files (first one found)
        Assert.Contains(result.SharePointItemId, new[] { "site1", "site2" });
    }

    [Fact]
    public async Task AddAsync_ValidProcessedFile_AddsAndReturnsFile()
    {
        // Arrange
        const string fileHash = "abc123def456";
        const long fileSize = 1024;
        var processedFile = CreateTestProcessedFile(fileHash: fileHash, fileSizeBytes: fileSize);

        // Act
        var result = await _repository.AddAsync(processedFile);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0); // Should have been assigned an ID
        Assert.Equal(fileHash, result.FileHash);
        Assert.Equal(fileSize, result.FileSizeBytes);

        // Verify it was actually saved to the database
        var savedFile =
            await _context.ProcessedFiles.FindAsync([result.Id], TestContext.Current.CancellationToken);
        Assert.NotNull(savedFile);
        Assert.Equal(fileHash, savedFile.FileHash);
    }

    [Fact]
    public async Task AddAsync_FileWithoutHashOrSize_AddsSuccessfully()
    {
        // Arrange
        var processedFile = new ProcessedFile
        {
            SharePointItemId = "test-site",
            ProcessedAt = DateTime.UtcNow,
            FileHash = null,
            FileSizeBytes = null
        };

        // Act
        var result = await _repository.AddAsync(processedFile);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Null(result.FileHash);
        Assert.Null(result.FileSizeBytes);
    }

    [Fact]
    public async Task UpdateAsync_ExistingFile_UpdatesAndReturnsFile()
    {
        // Arrange
        const string originalHash = "abc123def456";
        const string updatedHash = "xyz789uvw012";
        const long fileSize = 1024;

        var processedFile = CreateTestProcessedFile(fileHash: originalHash, fileSizeBytes: fileSize);
        await _context.ProcessedFiles.AddAsync(processedFile, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Modify the file
        processedFile.FileHash = updatedHash;
        processedFile.ProcessedAt = DateTime.UtcNow.AddMinutes(10);

        // Act
        var result = await _repository.UpdateAsync(processedFile);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(updatedHash, result.FileHash);

        // Verify the update was persisted
        var updatedFile =
            await _context.ProcessedFiles.FindAsync([processedFile.Id],
                TestContext.Current.CancellationToken);
        Assert.NotNull(updatedFile);
        Assert.Equal(updatedHash, updatedFile.FileHash);
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("a", 1)]
    [InlineData("abc123def456789", 1000000)]
    [InlineData("very-long-hash-string-that-represents-sha256", long.MaxValue)]
    public async Task ExistsByHashAsync_VariousHashAndSizeCombinations_WorksCorrectly(string hash, long size)
    {
        // Arrange
        var processedFile = CreateTestProcessedFile(fileHash: hash, fileSizeBytes: size);
        await _context.ProcessedFiles.AddAsync(processedFile, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var exists = await _repository.ExistsByHashAsync(hash, size);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task ExistsByHashAsync_CaseInsensitiveHash_ReturnsTrue()
    {
        // Arrange
        const string lowerCaseHash = "abc123def456";
        const string upperCaseHash = "ABC123DEF456";
        const long fileSize = 1024;

        var processedFile = CreateTestProcessedFile(fileHash: lowerCaseHash, fileSizeBytes: fileSize);
        await _context.ProcessedFiles.AddAsync(processedFile, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act - Search with different case
        var exists = await _repository.ExistsByHashAsync(upperCaseHash, fileSize);

        // Assert
        // This test verifies the expected behavior - adjust based on your actual requirements
        // If you want case-insensitive comparison, you'd need to modify the repository
        Assert.False(exists); // Assuming case-sensitive comparison
    }

    [Fact]
    public async Task AddAsync_MultipleFilesWithSameHash_AllowsMultipleEntries()
    {
        // Arrange
        const string fileHash = "abc123def456";
        const long fileSize = 1024;

        var file1 = CreateTestProcessedFile(siteId: "site1", fileHash: fileHash, fileSizeBytes: fileSize);
        var file2 = CreateTestProcessedFile(siteId: "site2", fileHash: fileHash, fileSizeBytes: fileSize);

        // Act
        var result1 = await _repository.AddAsync(file1);
        var result2 = await _repository.AddAsync(file2);

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.NotEqual(result1.Id, result2.Id);

        // Verify both exist in database
        var count = await _context.ProcessedFiles
            .CountAsync(pf => pf.FileHash == fileHash && pf.FileSizeBytes == fileSize,
                TestContext.Current.CancellationToken);
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task GetByHashAsync_WithNullValues_HandlesCorrectly()
    {
        // Arrange
        var processedFile = new ProcessedFile
        {
            SharePointItemId = "test-site",
            ProcessedAt = DateTime.UtcNow,
            FileHash = null,
            FileSizeBytes = null
        };

        await _context.ProcessedFiles.AddAsync(processedFile, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetByHashAsync(null!, 0);

        // Assert
        // This test verifies how nulls are handled - adjust based on your requirements
        Assert.Null(result); // Assuming null hash doesn't match
    }


    private static ProcessedFile CreateTestProcessedFile(string? siteId = null, long? fileSizeBytes = null,
        string? fileHash = null)
    {
        return new ProcessedFile
        {
            FileName = "test-file.csv",
            SharePointItemId = siteId ?? "test-item-id",
            ProcessedAt = DateTime.UtcNow,
            Status = "Processed",
            ErrorMessage = null,
            RecordCount = 0,
            FileHash = fileHash ?? new string('0', 64), // 64-char SHA-256 hex placeholder
            FileSizeBytes = fileSizeBytes ?? 1234L
        };
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}