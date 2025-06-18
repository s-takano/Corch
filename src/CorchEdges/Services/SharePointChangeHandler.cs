using System.Data.Common;
using CorchEdges.Abstractions;
using CorchEdges.Data;
using CorchEdges.Data.Abstractions;
using CorchEdges.Data.Entities;
using CorchEdges.Data.Repositories;
using CorchEdges.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace CorchEdges.Services;

// ─────────────────────────────────────────────────────────────────────────────
//  Orchestrator that can be unit‑tested in isolation
// ─────────────────────────────────────────────────────────────────────────────
/// <summary>
/// Handles and processes changes from SharePoint by coordinating multiple services
/// such as logging, Microsoft Graph interactions, database updates, and file parsing.
/// This class ensures the seamless handling of SharePoint-related notifications.
/// </summary>
public sealed class SharePointChangeHandler
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
    /// Represents a private field of type <c>IGraphFacade</c> used to interact with the Microsoft Graph API.
    /// </summary>
    /// <remarks>
    /// This field is utilized to perform operations such as testing connection, retrieving list items,
    /// drive items, and downloading content from Microsoft Graph. It is critical for handling operations
    /// against Microsoft Graph services in the <c>SharePointChangeHandler</c> class.
    /// </remarks>
    private readonly IGraphFacade _graph;

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
    private readonly IExcelParser _parser;

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

    private readonly ProcessingLogRepository _processingLogRepository;
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


    /// Handles and orchestrates changes specific to a SharePoint list or document library,
    /// enabling modular and maintainable integration with services such as logging,
    /// data parsing, database interactions, and API communication. The class uses dependency
    /// injection to ensure testability and maintain an isolated scope for unit testing.
    /// The constructor validates and initializes the required dependencies, database context,
    /// and identifiers for the SharePoint site, list, and monitored folder while ensuring
    /// proper format and integrity of the provided input parameters.
    public SharePointChangeHandler(
        ILogger log,
        IGraphFacade graph,
        IExcelParser parser,
        IDatabaseWriter db,
        EdgesDbContext context,
        ProcessingLogRepository processingLogRepository,
        IDataSetConverter dataSetConverter,
        string siteId,
        string listId,
        string watchedPath)
    {
        // Validate interface dependencies
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _graph = graph ?? throw new ArgumentNullException(nameof(graph));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _processingLogRepository =
            processingLogRepository ?? throw new ArgumentNullException(nameof(processingLogRepository));
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

    /// <summary>
    /// Asynchronously processes a SharePoint change notification to manage updates to relevant resources.
    /// </summary>
    /// <param name="notifications">a collection of the <see cref="SharePointNotification"/> object containing details about the resource and change metadata.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation, which resolves to a boolean indicating whether the operation succeeded.</returns>
    public async Task HandleAsync(IEnumerable<SharePointNotification> notifications)
    {
        var notificationList = notifications.ToList();
        var successfulItems = 0;
        var failedCount = 0;
        var hasErrors = false;
        string? lastError = null;

        foreach (var notification in notificationList) await ProcessNotificationAsync(notification);

        return;

        async Task ProcessNotificationAsync(SharePointNotification notification)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            var lastDataLink = await _processingLogRepository.GetDeltaLinkForSyncAsync(_siteId, _listId);

            var connection = _context.Database.GetDbConnection();
            try
            {
                _log.LogDebug("Processing change notification for resource: {resource}", notification.Resource);

                var (deltaLink, itemIds) = await _graph.PullItemsDeltaAsync(_siteId, _listId, lastDataLink);

                foreach (var itemId in itemIds)
                {
                    try
                    {
                        await ProcessItemAsync(itemId, connection, transaction);
                    }
                    catch (Exception ex)
                    {
                        _log.LogError(ex, 
                            "Fatal error processing notification for resource: {resource}, error item: {itemId}.",
                            notification.Resource, itemId);
                        throw;
                    }
                }

                // Create a new processing log for each notification
                await RecordProcessingLogAsync(successfulItems, failedCount, hasErrors, lastError, deltaLink);
                
                await transaction.CommitAsync();

                _log.LogInformation("Successfully processed {count} notifications", successfulItems);
            }
            catch (Exception e)
            {
                _log.LogWarning("Transaction failed: {EMessage}", e.Message);
                await transaction.RollbackAsync();
                throw;
            }
        }

        async Task ProcessItemAsync(string itemId, DbConnection connection, IDbContextTransaction transaction)
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

            // Filter by file extension before downloading
            if (!IsExcelFile(di.Name))
            {
                _log.LogInformation("Skipping non-Excel file: {fileName}", di.Name);
                return;
            }

            await using var stream = await _graph.DownloadAsync(di.ParentReference?.DriveId!, di.Id!);
            await using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            var bytes = ms.ToArray();

            var (ds, err) = _parser.Parse(bytes);
            if (!string.IsNullOrEmpty(err))
            {
                _log.LogError("Parser error: {error}", err);
                hasErrors = true;
                lastError = err;
                failedCount++;
                return;
            }

            if (ds == null)
            {
                _log.LogError("Parser returned null dataset");
                hasErrors = true;
                lastError = "Parser returned null dataset";
                failedCount++;
                return;
            }

            var preparedDataSet = _dataSetConverter.ConvertForDatabase(ds);

            await _db.WriteAsync(preparedDataSet, _context, connection, transaction.GetDbTransaction());

            successfulItems++;
        }
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
    private async Task RecordProcessingLogAsync(int successfulItems, int failedItems, bool hasErrors, string? lastError,
        string dataLink)
    {
        if (hasErrors)
            await _processingLogRepository.RecordFailedSyncAsync(_siteId, _listId, lastError!, failedItems);
        else
            await _processingLogRepository.RecordSuccessfulSyncAsync(_siteId, _listId, dataLink, successfulItems);
        
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