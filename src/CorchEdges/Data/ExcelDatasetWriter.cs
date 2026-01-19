using System.Data;
using System.Data.Common;
using CorchEdges.Data.Abstractions;
using CorchEdges.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Linq;
using System.Linq.Expressions;
using Org.BouncyCastle.Crypto.Engines;

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
    public async Task<int> WriteAsync(
        DataSet tables,
        EdgesDbContext context,
        DbConnection connection,
        DbTransaction transaction,
        int processingLogId)
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
                RecordCount = 0,
                ProcessingLogId = processingLogId
            };

            context.ProcessedFiles.Add(processedFile);
            await context.SaveChangesAsync(); // Still within transaction

            // 2. Add ProcessedFileId to each table
            StampProcessedFileId(tables, processedFile.Id);

            // 3. Get the underlying connection and use it for COPY
            var npgsqlConnection = (NpgsqlConnection)context.Database.GetDbConnection();

            
            // 4. Use PostgreSQL COPY with the SAME transaction
            await tableWriter.WriteAsync(tables, npgsqlConnection, transaction);

            // 5. Update metadata (still within the same transaction)
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


    public static void StampProcessedFileId(DataSet dataSet, int processedFileId)
    {
        const string columnName = nameof(ContractCreation.ProcessedFileId);

        foreach (var table in dataSet.Tables.Cast<DataTable>())
        {
            if (!table.Columns.Contains(columnName))
            {
                var col = new DataColumn(columnName, typeof(int)) { AllowDBNull = false };
                table.Columns.Add(col);
            }

            var colRef = table.Columns[columnName];
            colRef!.DefaultValue = processedFileId;

            table.BeginLoadData();
            try
            {
                foreach (DataRow row in table.Rows)
                {
                    if (row.IsNull(colRef)) row[colRef] = processedFileId;
                }
            }
            finally
            {
                table.EndLoadData();
            }
        }
    }
}