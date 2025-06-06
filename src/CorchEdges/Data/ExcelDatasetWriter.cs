using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CorchEdges.Data;

public class ExcelDatasetWriter(
    IPostgresTableWriter tableWriter,
    ILogger<ExcelDatasetWriter> logger)
    : IDatabaseWriter
{
    public async Task WriteAsync(DataSet tables, EdgesDbContext context, DbConnection connection, DbTransaction transaction)
    {
        var startTime = DateTime.Now;

        try
        {
            // 1. Create a metadata record (within transaction)
            var processedFile = new ProcessedFile
            {
                FileName = "Excel_Import",
                ProcessedAt = startTime,
                Status = "Processing",
                RecordCount = 0
            };

            context.ProcessedFiles.Add(processedFile);
            await context.SaveChangesAsync(); // Still within transaction

            // 2. Get the underlying connection and use it for COPY
            var npgsqlConnection = (NpgsqlConnection)context.Database.GetDbConnection();
            
            // 3. Use PostgreSQL COPY with the SAME transaction
            await tableWriter.WriteAsync(tables, npgsqlConnection, transaction);
            
            // 4. Update metadata (still within the same transaction)
            var totalRecords = tables.Tables.Cast<DataTable>().Sum(t => t.Rows.Count);
            processedFile.Status = "Success";
            processedFile.RecordCount = totalRecords;
            
            await context.SaveChangesAsync();
            
            // 5. Commit everything together
            await transaction.CommitAsync();
            
            var duration = DateTime.Now - startTime;
            logger.LogInformation(
                "Successfully processed {RecordCount} records in {Duration}ms using shared transaction", 
                totalRecords, duration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            // Rollback everything - no orphaned data!
            await transaction.RollbackAsync();
            logger.LogError(ex, "Failed to write DataSet - transaction rolled back");
            throw;
        }
    }

}