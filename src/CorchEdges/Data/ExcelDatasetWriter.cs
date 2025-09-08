using System.Data;
using System.Data.Common;
using CorchEdges.Data.Abstractions;
using CorchEdges.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CorchEdges.Data;

/// <summary>
/// Provides functionality for writing an Excel dataset to a PostgreSQL database using a transaction.
/// This class ensures data integrity by utilizing a single transaction for the metadata creation,
/// data writing, and metadata updates.
/// </summary>
public class ExcelDatasetWriter(
    IPostgresTableWriter tableWriter,
    ILogger<ExcelDatasetWriter> logger)
    : IDatabaseWriter
{
    /// <summary>
    /// Writes the provided dataset to the database within the given transaction, leveraging a PostgreSQL COPY operation.
    /// </summary>
    /// <param name="tables">A <see cref="DataSet"/> containing the tables to be written to the database.</param>
    /// <param name="context">The <see cref="EdgesDbContext"/> used for database context operations.</param>
    /// <param name="connection">The database connection to be used for the operation.</param>
    /// <param name="transaction">The database transaction that ensures atomicity of the operation.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous write operation.</returns>
    public async Task<int> WriteAsync(DataSet tables, EdgesDbContext context, DbConnection connection,
        DbTransaction transaction)
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

            var duration = DateTime.Now - startTime;
            logger.LogInformation(
                "Successfully processed {RecordCount} records in {Duration}ms using shared transaction",
                totalRecords, duration.TotalMilliseconds);
            
            return processedFile.Id;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to write DataSet - transaction rolled back");
            throw;
        }
    }
}