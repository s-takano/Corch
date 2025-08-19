using System.Security.Cryptography;

namespace CorchEdges.Utilities;

/// <summary>
/// Provides file hashing functionality for duplicate detection and integrity verification.
/// </summary>
public static class FileHashCalculator
{
    /// <summary>
    /// Calculates SHA-256 hash of the provided stream content.
    /// </summary>
    /// <param name="stream">The stream to calculate hash for.</param>
    /// <returns>A tuple containing the SHA-256 hash (lowercase hex string) and the stream size in bytes.</returns>
    public static async Task<(string hash, long size)> CalculateHashAsync(Stream stream)
    {
        if (!stream.CanSeek)
        {
            throw new ArgumentException("Stream must be seekable for hash calculation", nameof(stream));
        }

        var originalPosition = stream.Position;
        stream.Position = 0;
        
        try
        {
            using var sha256 = SHA256.Create();
            var hashBytes = await sha256.ComputeHashAsync(stream);
            var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();
            var size = stream.Length;
            
            return (hash, size);
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }

    /// <summary>
    /// Calculates SHA-256 hash of the provided byte array.
    /// </summary>
    /// <param name="content">The byte array to calculate hash for.</param>
    /// <returns>A tuple containing the SHA-256 hash (lowercase hex string) and the content size in bytes.</returns>
    public static (string hash, long size) CalculateHash(byte[] content)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(content);
        var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();
        
        return (hash, content.Length);
    }
}
