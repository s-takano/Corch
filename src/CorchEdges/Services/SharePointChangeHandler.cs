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
/// Represents a service responsible for managing and handling changes related to SharePoint
/// notifications. This class acts as an orchestrator coordinating various dependencies
/// such as graph connectivity, Excel parsing, database operations, and transaction management.
/// </summary>
public sealed class SharePointChangeHandler
{
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
    private static readonly Regex Rx = new(@"Items\((\d+)\)", RegexOptions.Compiled);

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

    /// <summary>
    /// The SharePointChangeHandler class orchestrates the coordination of SharePoint Graph operations,
    /// Excel file parsing, and database writing. It is designed for handling changes in a
    /// SharePoint list and can be easily unit-tested in isolation.
    /// </summary>
    public SharePointChangeHandler(
        ILogger log, 
        IGraphFacade graph, 
        IExcelParser parser, 
        IDatabaseWriter db, 
        EdgesDbContext context, 
        string siteId, 
        string listId)
    {
        _log = log; 
        _graph = graph; 
        _parser = parser; 
        _db = db;
        _context = context; 
        _siteId = siteId; 
        _listId = listId;
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
        // Start transaction
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var connection = _context.Database.GetDbConnection();
        
        try
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
            
            await _db.WriteAsync(ds!, _context, connection, transaction.GetDbTransaction());
            
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}