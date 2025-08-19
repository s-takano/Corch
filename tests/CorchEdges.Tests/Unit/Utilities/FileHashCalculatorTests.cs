using System.Security.Cryptography;
using System.Text;
using CorchEdges.Tests.Infrastructure;
using CorchEdges.Utilities;

namespace CorchEdges.Tests.Unit.Utilities;

[Trait("Category", TestCategories.Unit)]
[Trait("Component", "FileHashCalculator")]
public class FileHashCalculatorTests
{
    [Fact]
    public async Task CalculateHashAsync_ValidStream_ReturnsCorrectHashAndSize()
    {
        // Arrange
        var testContent = "Hello, World!"u8.ToArray();
        using var stream = new MemoryStream(testContent);
        
        // Calculate expected hash manually for verification
        using var sha256 = SHA256.Create();
        var expectedHashBytes = sha256.ComputeHash(testContent);
        var expectedHash = Convert.ToHexString(expectedHashBytes).ToLowerInvariant();

        // Act
        var (actualHash, actualSize) = await FileHashCalculator.CalculateHashAsync(stream);

        // Assert
        Assert.Equal(expectedHash, actualHash);
        Assert.Equal(testContent.Length, actualSize);
    }

    [Fact]
    public async Task CalculateHashAsync_EmptyStream_ReturnsCorrectHashAndSize()
    {
        // Arrange
        using var emptyStream = new MemoryStream();
        
        // Calculate expected hash for empty content
        using var sha256 = SHA256.Create();
        var expectedHashBytes = sha256.ComputeHash(Array.Empty<byte>());
        var expectedHash = Convert.ToHexString(expectedHashBytes).ToLowerInvariant();

        // Act
        var (actualHash, actualSize) = await FileHashCalculator.CalculateHashAsync(emptyStream);

        // Assert
        Assert.Equal(expectedHash, actualHash);
        Assert.Equal(0, actualSize);
    }

    [Fact]
    public async Task CalculateHashAsync_LargeStream_ReturnsCorrectHashAndSize()
    {
        // Arrange
        const int streamSize = 1024 * 1024; // 1MB
        var testContent = new byte[streamSize];
        Random.Shared.NextBytes(testContent);
        using var stream = new MemoryStream(testContent);

        // Calculate expected hash
        using var sha256 = SHA256.Create();
        var expectedHashBytes = sha256.ComputeHash(testContent);
        var expectedHash = Convert.ToHexString(expectedHashBytes).ToLowerInvariant();

        // Act
        var (actualHash, actualSize) = await FileHashCalculator.CalculateHashAsync(stream);

        // Assert
        Assert.Equal(expectedHash, actualHash);
        Assert.Equal(streamSize, actualSize);
    }

    [Fact]
    public async Task CalculateHashAsync_StreamAtNonZeroPosition_ResetsAndRestoresPosition()
    {
        // Arrange
        var testContent = "Hello, World!"u8.ToArray();
        using var stream = new MemoryStream(testContent);
        const int initialPosition = 5;
        stream.Position = initialPosition;

        // Act
        var (hash, size) = await FileHashCalculator.CalculateHashAsync(stream);

        // Assert
        Assert.Equal(initialPosition, stream.Position); // Position should be restored
        Assert.Equal(testContent.Length, size);
        Assert.NotEmpty(hash);
        Assert.Equal(64, hash.Length); // SHA-256 hex string length
    }

    [Fact]
    public async Task CalculateHashAsync_NonSeekableStream_ThrowsArgumentException()
    {
        // Arrange
        var mockStream = new NonSeekableMemoryStream("test content"u8.ToArray());

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => FileHashCalculator.CalculateHashAsync(mockStream));
        
        Assert.Contains("Stream must be seekable", exception.Message);
        Assert.Equal("stream", exception.ParamName);
    }

    [Fact]
    public async Task CalculateHashAsync_SameContent_ReturnsSameHash()
    {
        // Arrange
        var content1 = "Identical content"u8.ToArray();
        var content2 = "Identical content"u8.ToArray();
        using var stream1 = new MemoryStream(content1);
        using var stream2 = new MemoryStream(content2);

        // Act
        var (hash1, size1) = await FileHashCalculator.CalculateHashAsync(stream1);
        var (hash2, size2) = await FileHashCalculator.CalculateHashAsync(stream2);

        // Assert
        Assert.Equal(hash1, hash2);
        Assert.Equal(size1, size2);
    }

    [Fact]
    public async Task CalculateHashAsync_DifferentContent_ReturnsDifferentHashes()
    {
        // Arrange
        var content1 = "Content A"u8.ToArray();
        var content2 = "Content B"u8.ToArray();
        using var stream1 = new MemoryStream(content1);
        using var stream2 = new MemoryStream(content2);

        // Act
        var (hash1, _) = await FileHashCalculator.CalculateHashAsync(stream1);
        var (hash2, _) = await FileHashCalculator.CalculateHashAsync(stream2);

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void CalculateHash_ValidByteArray_ReturnsCorrectHashAndSize()
    {
        // Arrange
        var testContent = "Hello, World!"u8.ToArray();
        
        // Calculate expected hash
        using var sha256 = SHA256.Create();
        var expectedHashBytes = sha256.ComputeHash(testContent);
        var expectedHash = Convert.ToHexString(expectedHashBytes).ToLowerInvariant();

        // Act
        var (actualHash, actualSize) = FileHashCalculator.CalculateHash(testContent);

        // Assert
        Assert.Equal(expectedHash, actualHash);
        Assert.Equal(testContent.Length, actualSize);
    }

    [Fact]
    public void CalculateHash_EmptyByteArray_ReturnsCorrectHashAndSize()
    {
        // Arrange
        var emptyContent = Array.Empty<byte>();
        
        // Calculate expected hash for empty content
        using var sha256 = SHA256.Create();
        var expectedHashBytes = sha256.ComputeHash(emptyContent);
        var expectedHash = Convert.ToHexString(expectedHashBytes).ToLowerInvariant();

        // Act
        var (actualHash, actualSize) = FileHashCalculator.CalculateHash(emptyContent);

        // Assert
        Assert.Equal(expectedHash, actualHash);
        Assert.Equal(0, actualSize);
    }

    [Fact]
    public void CalculateHash_SameContent_ReturnsSameHash()
    {
        // Arrange
        var content1 = "Identical content"u8.ToArray();
        var content2 = "Identical content"u8.ToArray();

        // Act
        var (hash1, size1) = FileHashCalculator.CalculateHash(content1);
        var (hash2, size2) = FileHashCalculator.CalculateHash(content2);

        // Assert
        Assert.Equal(hash1, hash2);
        Assert.Equal(size1, size2);
    }

    [Fact]
    public void CalculateHash_DifferentContent_ReturnsDifferentHashes()
    {
        // Arrange
        var content1 = "Content A"u8.ToArray();
        var content2 = "Content B"u8.ToArray();

        // Act
        var (hash1, _) = FileHashCalculator.CalculateHash(content1);
        var (hash2, _) = FileHashCalculator.CalculateHash(content2);

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void CalculateHash_LargeByteArray_ReturnsCorrectHashAndSize()
    {
        // Arrange
        const int arraySize = 1024 * 1024; // 1MB
        var testContent = new byte[arraySize];
        Random.Shared.NextBytes(testContent);

        // Calculate expected hash
        using var sha256 = SHA256.Create();
        var expectedHashBytes = sha256.ComputeHash(testContent);
        var expectedHash = Convert.ToHexString(expectedHashBytes).ToLowerInvariant();

        // Act
        var (actualHash, actualSize) = FileHashCalculator.CalculateHash(testContent);

        // Assert
        Assert.Equal(expectedHash, actualHash);
        Assert.Equal(arraySize, actualSize);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("Hello World")]
    [InlineData("This is a longer test string with various characters: !@#$%^&*()")]
    public async Task CalculateHashAsync_VariousStringInputs_ReturnsValidHash(string input)
    {
        // Arrange
        var content = Encoding.UTF8.GetBytes(input);
        using var stream = new MemoryStream(content);

        // Act
        var (hash, size) = await FileHashCalculator.CalculateHashAsync(stream);

        // Assert
        Assert.NotNull(hash);
        Assert.Equal(64, hash.Length); // SHA-256 produces 64-character hex string
        Assert.True(hash.All(c => char.IsAsciiHexDigitLower(c)), "Hash should contain only lowercase hex characters");
        Assert.Equal(content.Length, size);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("Hello World")]
    [InlineData("This is a longer test string with various characters: !@#$%^&*()")]
    public void CalculateHash_VariousStringInputs_ReturnsValidHash(string input)
    {
        // Arrange
        var content = Encoding.UTF8.GetBytes(input);

        // Act
        var (hash, size) = FileHashCalculator.CalculateHash(content);

        // Assert
        Assert.NotNull(hash);
        Assert.Equal(64, hash.Length); // SHA-256 produces 64-character hex string
        Assert.True(hash.All(c => char.IsAsciiHexDigitLower(c)), "Hash should contain only lowercase hex characters");
        Assert.Equal(content.Length, size);
    }

    [Fact]
    public async Task CalculateHashAsync_StreamAndByteArray_ProduceSameResults()
    {
        // Arrange
        var testContent = "Test content for comparison"u8.ToArray();
        using var stream = new MemoryStream(testContent);

        // Act
        var (streamHash, streamSize) = await FileHashCalculator.CalculateHashAsync(stream);
        var (arrayHash, arraySize) = FileHashCalculator.CalculateHash(testContent);

        // Assert
        Assert.Equal(streamHash, arrayHash);
        Assert.Equal(streamSize, arraySize);
    }

    // Helper class for testing non-seekable streams
    private class NonSeekableMemoryStream : MemoryStream
    {
        public NonSeekableMemoryStream(byte[] buffer) : base(buffer) { }
        
        public override bool CanSeek => false;
        
        public override long Position 
        { 
            get => throw new NotSupportedException(); 
            set => throw new NotSupportedException(); 
        }
    }
}
