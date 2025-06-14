using System.Text.RegularExpressions;
using CorchEdges.Abstractions;
using CorchEdges.Data;
using CorchEdges.Data.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Models;

namespace CorchEdges;

// ─────────────────────────────────────────────────────────────────────────────
//  Orchestrator that can be unit‑tested in isolation
// ─────────────────────────────────────────────────────────────────────────────
/// <summary>
/// Represents an orchestrator for handling SharePoint-related changes and notifications.
/// This service coordinates multiple dependencies, such as logging, Microsoft Graph
/// operations, Excel file parsing, database updates, and transactional workflows.
/// </summary>
public sealed class SharePointChangeHandler
{
    private readonly string _watchedPath;

    /// <summary>
    /// Represents the logging service used for recording essential information,
    /// such as errors, warnings, and informational messages, during the execution
    /// of processes within the application.
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
    /// An instance of the <see cref="IExcelParser"/> interface, used to parse
    /// Excel file byte data into a structured <see cref="System.Data.DataSet"/>
    /// for further processing.
    /// </summary>
    /// <remarks>
    /// This parser is utilized within the <see cref="SharePointChangeHandler"/> class
    /// to process binary data representing Excel files downloaded via the
    /// Microsoft Graph API. Parsing results include the structured data
    /// and any potential parsing errors.
    /// </remarks>
    private readonly IExcelParser _parser;

    /// <summary>
    /// Represents a database writer for persisting data to the database.
    /// Used to perform write operations asynchronously with support for database
    /// context, connections, and transactions.
    /// </summary>
    private readonly IDatabaseWriter _db;

    /// <summary>
    /// Represents an instance of the <see cref="EdgesDbContext"/> that is utilized for database operations
    /// within the <see cref="SharePointChangeHandler"/> class during the processing of changes and notifications.
    /// </summary>
    /// <remarks>
    /// This variable is used to interact with the database, manage transactions, and persist data.
    /// It provides access to various DbSet properties defined in the <see cref="EdgesDbContext"/> class
    /// for handling domain entities such as processed files, logs, contracts, and related operations.
    /// The context is passed to the <see cref="SharePointChangeHandler"/> through dependency injection
    /// and is used to ensure proper database interactions, including starting transactions,
    /// committing changes, or rolling back modifications in case of failures during handling of changes.
    /// </remarks>
    private readonly EdgesDbContext _context;

    /// <summary>
    /// Represents a compiled regular expression used to match and extract numeric item identifiers from a string
    /// with the pattern "Items(d+)".
    /// </summary>
    /// <remarks>
    /// The regular expression is optimized for performance using the RegexOptions.Compiled flag. It is primarily
    /// designed to be used in scenarios where extracting item IDs from change notifications is required, as seen in
    /// processes like handling Graph API change notifications.
    /// </remarks>
    private static readonly Regex Rx = new(@"items/(\d+)$", RegexOptions.Compiled);

    /// <summary>
    /// Represents the unique identifier for a SharePoint site used to interact with the Microsoft Graph API.
    /// This identifier is utilized for operations, such as retrieving list items, accessing drive items, and performing
    /// other site-specific actions within the given site context.
    /// </summary>
    private readonly string _siteId;

    /// <summary>
    /// Identifier for the target list within the specified site in Microsoft Graph.
    /// </summary>
    private readonly string _listId;


    /// Handles and orchestrates changes specific to a SharePoint list or document library,
    /// allowing for modular and testable integration of various dependencies such as logging,
    /// data parsing, database operations, and API interactions.
    /// The class is designed to enable isolated unit testing by relying on dependency
    /// injection for its core functionalities.
    /// Constructor of the class initializes the required services, database context,
    /// and identifiers pertinent to the SharePoint site, list, and watched folder.
    public SharePointChangeHandler(
        ILogger log,
        IGraphFacade graph,
        IExcelParser parser,
        IDatabaseWriter db,
        EdgesDbContext context,
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
    private static bool IsValidGuid(string value)
    {
        return Guid.TryParse(value, out _);
    }

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

    /// Ensures a valid connection to the Microsoft Graph API by testing the connectivity
    /// and handling any errors that might occur. Logs the connection status and specific
    /// error details if the connection fails.
    /// The method attempts to perform error-specific logging to guide resolution steps.
    /// Returns true if the connection is successful, otherwise false.
    /// <returns>
    /// A task representing the asynchronous operation. Returns true if the Graph API
    /// connection is valid, otherwise false.
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
    /// Handles a change notification by processing the corresponding resource and applying updates through the database.
    /// </summary>
    /// <param name="change">The <see cref="ChangeNotification"/> instance containing the resource and metadata related to the change.</param>
    /// <returns>A <see cref="Task"/> that resolves to a boolean indicating the success or failure of the operation.</returns>
    public async Task HandleAsync(ChangeNotification change)
    {
        string itemId = Rx.Match(change.Resource ?? "").Groups[1].Value;
        if (string.IsNullOrEmpty(itemId))
        {
            _log.LogWarning("Bad resource {r}", change.Resource);
            return;
        }

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
            _log.LogInformation("Skipping item outside watched folder: {p}", parentPathRaw);
            return;             // everything else stays the same
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
        if (err != null)
        {
            _log.LogError(err);
            return;
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Start transaction
            var connection = _context.Database.GetDbConnection();

            await _db.WriteAsync(ds!, _context, connection, transaction.GetDbTransaction());

            await transaction.CommitAsync();
        }
        catch (Exception e)
        {
            _log.LogWarning("Database Writer failed: {EMessage}", e.Message);
            await transaction.RollbackAsync();
            throw;
        }
    }

    
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
            .TrimEnd('/')                    // no trailing slash
            .ToLowerInvariant();
    }

    /// <summary>
    /// Determines whether the specified file name represents an Excel file based on its extension.
    /// </summary>
    /// <param name="fileName">The name of the file to check.</param>
    /// <returns>True if the file is an Excel file; otherwise, false.</returns>
    private static bool IsExcelFile(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return false;

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension is ".xlsx" or ".xls" or ".xlsm" or ".xlsb";
    }
}