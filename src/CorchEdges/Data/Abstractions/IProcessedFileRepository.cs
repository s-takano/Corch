using CorchEdges.Data.Entities;

namespace CorchEdges.Data.Repositories;

/// <summary>
/// Repository interface for ProcessedFile operations.
/// </summary>
public interface IProcessedFileRepository
{
    Task<bool> ExistsByHashAsync(string fileHash, long fileSize);
    Task<ProcessedFile?> GetByHashAsync(string fileHash, long fileSize);
    Task<ProcessedFile> AddAsync(ProcessedFile processedFile);
    Task<ProcessedFile> UpdateAsync(ProcessedFile processedFile);
}