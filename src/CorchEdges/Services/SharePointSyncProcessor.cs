using System.Data.Common;
using CorchEdges.Abstractions;
using CorchEdges.Data;
using CorchEdges.Data.Abstractions;
using CorchEdges.Data.Entities;
using CorchEdges.Data.Repositories;
using CorchEdges.Models;
using CorchEdges.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Models.ODataErrors;

namespace CorchEdges.Services;

// ─────────────────────────────────────────────────────────────────────────────
//  Orchestrator that can be unit‑tested in isolation
// ─────────────────────────────────────────────────────────────────────────────
/// <summary>
/// Handles and processes changes from SharePoint by coordinating multiple services
/// such as logging, Microsoft Graph interactions, database updates, and file parsing.
/// This class ensures the seamless handling of SharePoint-related notifications.
/// </summary>
public class SharePointSyncProcessor : ISharePointSyncProcessor
{
    /// <summary>
    /// Represents the path of the watched folder in a SharePoint drive. This is used
    /// to filter and process changes to items located within the specified folder path.
    /// </summary>
    private readonly string _watchedPath;

    /// <summary>
    /// Provides a logging service instance for capturing and recording events, errors,
    /// warnings, and general information throughout the lifecycle of the class.
    /// </summary>
    private readonly ILogger _log;

    /// <summary>
    /// Represents a private field of type <c>IGraphApiClient</c> used to interact with the Microsoft Graph API.
    /// </summary>
    /// <remarks>
    /// This field is utilized to perform operations such as testing connection, retrieving list items,
    /// drive items, and downloading content from Microsoft Graph. It is critical for handling operations
    /// against Microsoft Graph services in the <c>SharePointSyncProcessor</c> class.
    /// </remarks>
    private readonly IGraphApiClient _graph;

    /// <summary>
    /// Represents the parser used for converting Excel file byte data into
    /// a structured dataset, enabling downstream processing of the data
    /// within the business logic.
    /// </summary>
    /// <remarks>
    /// This component is critical for handling Excel file data retrieved from
    /// external systems, ensuring accurate and reliable data parsing for further
    /// operations.
    /// </remarks>
    private readonly ITabularDataParser _parser;

    /// <summary>
    /// Provides access to a database writer for handling data persistence operations.
    /// This variable is used to asynchronously write data to the database, integrating with the
    /// application's database context, managing connections and transactions.
    /// </summary>
    private readonly IDatabaseWriter _db;

    /// <summary>
    /// Represents the database context used for interacting with the data layer,
    /// enabling the execution of data operations such as querying, saving, and updating entities.
    /// </summary>
    /// <remarks>
    /// This field is critical for managing the application's connection to the underlying database
    /// and for performing operations on domain models. It is instantiated through dependency injection
    /// to ensure lifecycle management and testability.
    /// </remarks>
    private readonly EdgesDbContext _context;

    private readonly IProcessingLogRepository _processingLogRepository;
    private readonly IProcessedFileRepository _processedFileRepository;
    private readonly IDataSetConverter _dataSetConverter;

    /// <summary>
    /// Represents the unique identifier of a SharePoint site, used for identifying
    /// the specific site within operations leveraging the Microsoft Graph API or
    /// other related integrations. The value is crucial for performing actions such
    /// as accessing site-specific resources, lists, or folder paths.
    /// </summary>
    private readonly string _siteId;

    /// <summary>
    /// Stores the unique identifier of the target SharePoint list to be monitored,
    /// typically used for tracking changes or accessing the list within a specified site.
    /// </summary>
    private readonly string _listId;

    public int SuccessfulItems { get; private set; }
    public int FailedCount { get; private set; }
    public bool HasErrors { get; private set; }
    public string? LastError { get; private set; }


    /// Handles and orchestrates changes specific to a SharePoint list or document library,
    /// enabling modular and maintainable integration with services such as logging,
    /// data parsing, database interactions, and API communication. The class uses dependency
    /// injection to ensure testability and maintain an isolated scope for unit testing.
    /// The constructor validates and initializes the required dependencies, database context,
    /// and identifiers for the SharePoint site, list, and monitored folder while ensuring
    /// proper format and integrity of the provided input parameters.
    public SharePointSyncProcessor(
        ILogger log,
        IGraphApiClient graph,
        ITabularDataParser parser,
        IDatabaseWriter db,
        EdgesDbContext context,
        IProcessingLogRepository processingLogRepository,
        IProcessedFileRepository processedFileRepository,
        IDataSetConverter dataSetConverter,
        string siteId,
        string listId,
        string watchedPath)
    {
        SuccessfulItems = 0;
        FailedCount = 0;
        HasErrors = false;
        LastError = null;

        // Validate interface dependencies
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _graph = graph ?? throw new ArgumentNullException(nameof(graph));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _processingLogRepository =
            processingLogRepository ?? throw new ArgumentNullException(nameof(processingLogRepository));
        _processedFileRepository =
            processedFileRepository ?? throw new ArgumentNullException(nameof(processedFileRepository));
        _dataSetConverter = dataSetConverter ?? throw new ArgumentNullException(nameof(dataSetConverter));

        // Validate string parameters
        if (string.IsNullOrWhiteSpace(siteId))
            throw new ArgumentException("Site ID cannot be null or whitespace.", nameof(siteId));

        if (string.IsNullOrWhiteSpace(listId))
            throw new ArgumentException("List ID cannot be null or whitespace.", nameof(listId));

        if (string.IsNullOrWhiteSpace(watchedPath))
            throw new ArgumentException("Watched folder drive ID cannot be null or whitespace.", nameof(watchedPath));

        // Additional validation for ID formats (optional but recommended)
        if (!IsValidGuid(siteId) && !IsValidSharePointId(siteId))
            throw new ArgumentException("Site ID must be a valid GUID or SharePoint ID format.", nameof(siteId));

        if (!IsValidGuid(listId))
            throw new ArgumentException("List ID must be a valid GUID.", nameof(listId));

        if (string.IsNullOrEmpty(watchedPath))
            throw new ArgumentException("Watched folder drive path must be a valid GUID.", nameof(watchedPath));

        // Assign validated parameters
        _siteId = siteId;
        _listId = listId;
        _watchedPath = watchedPath;
    }

    // Helper methods for validation
    /// Validates whether the specified string is a valid GUID by attempting to parse it.
    /// This helper method can be used for verification purposes when dealing with
    /// identifiers that must adhere to the GUID format.
    /// <param name="value">The string to validate as a GUID.</param>
    /// <returns>True if the input string is a valid GUID; otherwise, false.</returns>
    private static bool IsValidGuid(string value)
    {
        return Guid.TryParse(value, out _);
    }

    /// Validates whether the given string conforms to the expected SharePoint ID format,
    /// which typically consists of a hostname followed by two GUIDs separated by commas.
    /// Example format: "hostname,guid,guid".
    /// This method checks for both the structure of the string and the validity of the
    /// GUIDs in the second and third segments.
    /// <param name="value">The string to validate as a potential SharePoint ID.</param>
    /// <returns>
    /// True if the string is non-empty and follows the expected SharePoint ID format;
    /// false otherwise.
    /// </returns>
    private static bool IsValidSharePointId(string value)
    {
        // SharePoint site IDs often follow the pattern: hostname,guid,guid
        // Example: contoso.sharepoint.com,12345678-1234-1234-1234-123456789012,87654321-4321-4321-4321-210987654321
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var parts = value.Split(',');
        if (parts.Length == 3)
        {
            // First part should be hostname, second and third should be GUIDs
            return !string.IsNullOrWhiteSpace(parts[0]) &&
                   IsValidGuid(parts[1]) &&
                   IsValidGuid(parts[2]);
        }

        return false;
    }

    /// Ensures a valid connection to the Microsoft Graph API by verifying connectivity
    /// and handling any connection issues. Logs the status and provides detailed error
    /// information to assist with troubleshooting if the connection attempt fails.
    /// Intended to validate Graph API availability before proceeding with subsequent
    /// operations.
    /// <returns>
    /// A task representing the asynchronous operation. Returns true if the connection
    /// to the Microsoft Graph API is successful, otherwise false.
    /// </returns>
    public async Task<bool> EnsureGraphConnectionAsync()
    {
        var result = await _graph.TestConnectionAsync();

        if (!result.IsSuccess)
        {
            _log.LogError("Graph connection failed: {reason} (Code: {code})",
                result.ErrorReason, result.ErrorCode);

            // Take specific action based on error type
            switch (result.ErrorCode)
            {
                case "Forbidden":
                    _log.LogError("Check Azure AD app permissions");
                    break;
                case "AuthenticationFailed":
                    _log.LogError("Check credentials and tenant configuration");
                    break;
                case "Timeout":
                    _log.LogWarning("Graph API is slow, consider retry");
                    break;
            }

            return false;
        }

        _log.LogInformation("Graph connection successful");
        return true;
    }

    public async Task<SharePointSyncResult> FetchAndStoreDeltaAsync(int batchSize = int.MaxValue)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        var lastDataLink = await _processingLogRepository.GetDeltaLinkForSyncAsync(_siteId, _listId);

        var connection = _context.Database.GetDbConnection();
        try
        {
            _log.LogDebug("Pulling items delta");

            List<string> itemIds;
            string deltaLink;

            try
            {
                (deltaLink, itemIds) = await _graph.PullItemsDeltaAsync(_siteId, _listId, lastDataLink);
            }
            catch (ODataError ex) when (IsResyncRequired(ex))
            {
                _log.LogWarning("Delta link expired. Attempting windowed resync from last_processed_at.");

                var lastProcessedAt = await _processingLogRepository.GetLastProcessedAtUtcAsync(_siteId, _listId);

                if (lastProcessedAt == null)
                {
                    _log.LogWarning("No last_processed_at available. Establishing fresh delta link.");
                    deltaLink = await _graph.GetFreshDeltaLinkAsync(_siteId, _listId);
                    itemIds = new List<string>();
                }
                else
                {
                    _log.LogInformation("Performing windowed resync from last_processed_at: {lastProcessedAt}",
                        lastProcessedAt.Value);

                    var windowStart = lastProcessedAt.Value.AddMinutes(-10);
                    itemIds = await _graph.PullItemsModifiedSinceAsync(_siteId, _listId, windowStart);
                    deltaLink = await _graph.GetFreshDeltaLinkAsync(_siteId, _listId);
                }
            }

            if (itemIds.Count == 0)
            {
                var log = await _processingLogRepository.CreateProcessingLogAsync(_siteId, _listId);
                await RecordProcessingLogAsync(
                    log.Id, SuccessfulItems, FailedCount, HasErrors, LastError, deltaLink);

                await transaction.CommitAsync();
                return SharePointSyncResult.Succeeded();
            }

            var batch = itemIds.Take(batchSize).ToList();
            var remaining = itemIds.Skip(batchSize).ToList();

            var batchResult = await FetchAndStoreItemsAsync(
                batch,
                deltaLink,
                finalizeDeltaLink: remaining.Count == 0,
                connection,
                transaction);
            
            await transaction.CommitAsync();

            if (remaining.Count > 0)
            {
                return batchResult with
                {
                    RemainingItemIds = remaining,
                    PendingDeltaLink = deltaLink
                };
            }

            return batchResult;
        }
        catch (Exception e)
        {
            _log.LogWarning("Transaction failed: {EMessage}", e.Message);
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<SharePointSyncResult> FetchAndStoreItemsAsync(IReadOnlyList<string> itemIds, string deltaLink,
        bool finalizeDeltaLink,
        int batchSize = Int32.MaxValue)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var connection = _context.Database.GetDbConnection();

        try
        {
            var fetchAndStoreItemsAsync = await FetchAndStoreItemsAsync(
                itemIds, deltaLink, finalizeDeltaLink, connection, transaction,
                batchSize);

            await transaction.CommitAsync();

            return fetchAndStoreItemsAsync;
        }
        catch (Exception e)
        {
            _log.LogWarning("Transaction failed: {EMessage}", e.Message);
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<SharePointSyncResult> FetchAndStoreItemsAsync(
        IReadOnlyList<string> itemIds,
        string deltaLink,
        bool finalizeDeltaLink,
        DbConnection connection,
        IDbContextTransaction transaction,
        int batchSize = int.MaxValue)
    {
        try
        {
            _log.LogInformation("Processing {count} items from delta", itemIds.Count);

            var batch = itemIds.Take(batchSize).ToList();
            var remaining = itemIds.Skip(batchSize).ToList();

            var log = await _processingLogRepository.CreateProcessingLogAsync(_siteId, _listId);

            foreach (var itemId in batch)
            {
                try
                {
                    await FetchAndStoreItemAsync(itemId, connection, transaction, log.Id);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex,
                        "Fatal error processing an item, error item: {itemId}.",
                        itemId);
                    throw;
                }
            }

            await RecordProcessingLogAsync(
                log.Id, SuccessfulItems, FailedCount, HasErrors, LastError, deltaLink);

            var result = SharePointSyncResult.Succeeded();

            if (remaining.Count > 0)
            {
                return result with
                {
                    RemainingItemIds = remaining,
                    PendingDeltaLink = deltaLink
                };
            }

            return result;
        }
        catch (Exception e)
        {
            _log.LogWarning("Transaction failed: {EMessage}", e.Message);
            throw;
        }
    }

    private static bool IsResyncRequired(ODataError ex)
    {
        var message = ex.Error?.Message ?? string.Empty;
        return message.Contains("ResyncApplyDifferencesVroomException", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> IsFileDuplicateAsync(string fileHash, long fileSize)
    {
        return await _processedFileRepository.ExistsByHashAsync(fileHash, fileSize);
    }

        private async Task FetchAndStoreItemAsync(
            string itemId,
            DbConnection connection,
            IDbContextTransaction transaction,
            int processingLogId)
        {
        var li = await _graph.GetListItemAsync(_siteId, _listId, itemId);
        if (li?.Fields?.AdditionalData?.TryGetValue("ProcessFlag", out var flag) == true &&
            !"Yes".Equals(flag?.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            _log.LogInformation("Skipping {id}", itemId);
            return;
        }

        var di = await _graph.GetDriveItemAsync(_siteId, _listId, itemId) ??
                 throw new InvalidOperationException("drive item null");

        // Filter by watched folder
        var parentPathRaw = di.ParentReference?.Path;
        if (parentPathRaw is null)
        {
            _log.LogWarning("DriveItem has no parent path. Id={id}", di.Id);
            return;
        }

        var parentCanon = Canon(parentPathRaw);
        var watchedCanon = Canon(_watchedPath);

        if (parentCanon != watchedCanon)
        {
            _log.LogInformation("Skipping item outside watched folder: {p}", parentCanon);
            return;
        }

        if (di.Name == null)
            throw new InvalidOperationException("DriveItem has no name");

        // Filter by file extension before downloading
        if (!IsExcelFile(di.Name))
        {
            _log.LogInformation("Skipping non-Excel file: {fileName}", di.Name);
            return;
        }

        _log.LogInformation("Downloading file: {fileName} from drive: {driveId}, item: {itemId}",
            di.Name, di.ParentReference?.DriveId, di.Id);

        await using var stream = await _graph.DownloadAsync(di.ParentReference?.DriveId!, di.Id!);

        _log.LogInformation("Download completed for file: {fileName}. Processing stream...", di.Name);

        if (!await WriteStreamAsync(connection, transaction, stream, di.Name, processingLogId))
        {
            _log.LogInformation("File {fileName} was not processed (duplicate or failed)", di.Name);
            return;
        }

        _log.LogInformation("Successfully processed file: {fileName}", di.Name);

        SuccessfulItems++;
    }

    private async Task<bool> WriteStreamAsync(
            DbConnection connection,
            IDbContextTransaction transaction,
            Stream stream,
            string fileName,
            int processingLogId)
    {
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);

        // Filter by file duplication by comparing the hash of each file to the hashes of past files
        // which have already been uploaded 
        var (fileHash, fileSize) = await FileHashCalculator.CalculateHashAsync(memoryStream);

        if (await IsFileDuplicateAsync(fileHash, fileSize))
        {
            _log.LogInformation("Duplicate file detected with hash {Hash} and size {Size} bytes",
                fileHash, fileSize);
            return false;
        }

        memoryStream.Seek(0, SeekOrigin.Begin);

        // only new file is processed

        var (ds, err) = _parser.Parse(memoryStream);
        if (!string.IsNullOrEmpty(err))
        {
            _log.LogError("Parser error: {error}", err);
            HasErrors = true;
            LastError = err;
            FailedCount++;
            return false;
        }

        if (ds == null)
        {
            _log.LogError("Parser returned null dataset");
            HasErrors = true;
            LastError = "Parser returned null dataset";
            FailedCount++;
            return false;
        }

        var preparedDataSet = _dataSetConverter.ConvertForDatabase(ds);

        var id = await _db.WriteAsync(
                preparedDataSet, _context, connection, transaction.GetDbTransaction(), processingLogId);

        await UpdateProcessedFileMetadata(id, fileHash, fileSize, fileName);

        return true;
    }

    private async Task UpdateProcessedFileMetadata(int id, string fileHash, long fileSize, string fileName)
    {
        var file = await _processedFileRepository.GetByIdAsync(id);
        if (file == null)
            throw new InvalidOperationException("File not found");

        file.FileName = fileName;
        file.FileHash = fileHash;
        file.FileSizeBytes = fileSize;

        await _processedFileRepository.UpdateAsync(file);
    }

    /// <summary>
    /// Creates a new processing log record in the database to track the processing results.
    /// A new log is created for each HandleAsync call.
    /// </summary>
    /// <param name="successfulItems">Number of notifications successfully processed</param>
    /// <param name="failedItems">Number of notifications that failed to process</param>
    /// <param name="hasErrors">Whether any errors occurred during processing</param>
    /// <param name="lastError">The last error message if any errors occurred</param>
    /// <param name="dataLink"></param>
        private async Task RecordProcessingLogAsync(
            int processingLogId,
            int successfulItems,
            int failedItems,
            bool hasErrors,
            string? lastError,
            string dataLink)
        {
            await _processingLogRepository.UpdateProcessingLogAsync(
                processingLogId,
                dataLink,
                successfulItems,
                failedItems,
                hasErrors,
                lastError);

            _log.LogDebug("Processing log created successfully");
        }


    /// <summary>
    /// Normalizes a raw file path by applying specific transformations, such as stripping out
    /// prefixes, URL-decoding, replacing backward slashes with forward slashes, and converting
    /// the path to lowercase. The method ensures consistent formatting of paths for comparison purposes.
    /// </summary>
    /// <param name="raw">The raw path string to normalize, typically in the format returned by Microsoft Graph.</param>
    /// <returns>A normalized path string with consistent formatting, suitable for further processing or comparison.</returns>
    private static string Canon(string raw)
    {
        // Graph gives something like "/sites/Fin/drive/root:/Shared%20Documents/Accounting"
        // 1) cut off everything up to the first ':'   ("/Shared%20Documents/Accounting")
        // 2) URL-decode (%20 → space)
        // 3) force forward-slashes and lower-case
        int idx = raw.IndexOf(':');
        if (idx >= 0 && idx + 1 < raw.Length) raw = raw[(idx + 1)..];
        return Uri.UnescapeDataString(raw)
            .Replace('\\', '/')
            .TrimEnd('/') // no trailing slash
            .ToLowerInvariant();
    }

    /// <summary>
    /// Determines whether the specified file name represents an Excel file based on its extension.
    /// </summary>
    /// <param name="fileName">The name of the file to evaluate. Can be null or empty.</param>
    /// <returns>True if the file name ends with a supported Excel file extension, otherwise false.</returns>
    private static bool IsExcelFile(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return false;

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension is ".xlsx" or ".xls" or ".xlsm" or ".xlsb";
    }
}