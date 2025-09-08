using CorchEdges.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CorchEdges.Data.Repositories;

/// <summary>
/// Repository implementation for ProcessedFile operations.
/// </summary>
public class ProcessedFileRepository : IProcessedFileRepository
{
    private readonly EdgesDbContext _context;

    public ProcessedFileRepository(EdgesDbContext context)
    {
        _context = context;
    }

    public async Task<bool> ExistsByHashAsync(string fileHash, long fileSize)
    {
        return await _context.ProcessedFiles
            .AnyAsync(pf => pf.FileHash == fileHash && pf.FileSizeBytes == fileSize);
    }

    public async Task<ProcessedFile?> GetByHashAsync(string fileHash, long fileSize)
    {
        return await _context.ProcessedFiles
            .FirstOrDefaultAsync(pf => pf.FileHash == fileHash && pf.FileSizeBytes == fileSize);
    }

    public async Task<ProcessedFile> AddAsync(ProcessedFile processedFile)
    {
        _context.ProcessedFiles.Add(processedFile);
        await _context.SaveChangesAsync();
        return processedFile;
    }
    

    public async Task<ProcessedFile> UpdateAsync(ProcessedFile processedFile)
    {
        _context.ProcessedFiles.Update(processedFile);
        await _context.SaveChangesAsync();
        return processedFile;
    }

    public async Task<ProcessedFile?> GetByIdAsync(int id)
    {
        return await _context.ProcessedFiles.FindAsync(id);
    }
}
