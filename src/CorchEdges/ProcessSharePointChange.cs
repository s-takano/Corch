using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Azure.Storage.Blobs;
using CorchEdges.Data;
using CorchEdges.Data.Abstractions;
using ExcelDataReader;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Models;

namespace CorchEdges;

file sealed class NotificationEnvelope
{
    [JsonPropertyName("value")] public ChangeNotification[] Value { get; set; } = [];
}


// ─────────────────────────────────────────────────────────────────────────────
//  Orchestrator that can be unit‑tested in isolation
// ─────────────────────────────────────────────────────────────────────────────
public sealed class ChangeHandler
{
    private readonly ILogger _log;
    private readonly IGraphFacade _graph;
    private readonly IExcelParser _parser;
    private readonly IDatabaseWriter _db;
    private readonly EdgesDbContext _context; 

    private static readonly Regex Rx = new(@"Items\((\d+)\)", RegexOptions.Compiled);
    private readonly string _siteId;
    private readonly string _listId;

    public ChangeHandler(
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
        _context = context; // Add this
        _siteId = siteId; 
        _listId = listId;
    }

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

// ─────────────────────────────────────────────────────────────────────────────
//  Azure Function – thin, DI‑friendly wrapper
// ─────────────────────────────────────────────────────────────────────────────
public sealed class ProcessSharePointChange
{
    private readonly ILogger<ProcessSharePointChange> _log;
    private readonly ChangeHandler _handler;
    private readonly BlobContainerClient _failed;

    public ProcessSharePointChange(ILogger<ProcessSharePointChange> log, ChangeHandler handler, BlobServiceClient blobs)
    {
        _log = log; 
        _handler = handler; 
        _failed = blobs.GetBlobContainerClient("failed-changes");
    }

    [Function(nameof(ProcessSharePointChange))]
    public async Task RunAsync([ServiceBusTrigger("sp-changes", Connection="ServiceBusConnection")] string msg)
    {
        try
        {
            // 🔍 Step 1: Verify Graph connection before processing
            _log.LogInformation("Verifying Graph API connection...");
            
            var connectionValid = await _handler.EnsureGraphConnectionAsync();
            if (!connectionValid)
            {
                _log.LogError("Graph API connection failed - aborting message processing");
                
                // Save the message to the failed blob for manual retry when the connection is restored
                var blob = $"graph-connection-failed-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.json";
                await _failed.UploadBlobAsync(blob, BinaryData.FromString(msg));
                _log.LogWarning("Message saved to {blob} for retry when Graph connection is restored", blob);
                
                // Don't throw - this prevents infinite retries for credential issues
                // The message is safely stored in blob storage for manual processing
                return;
            }

            // 📨 Step 2: Process the notification message
            _log.LogInformation("Processing SharePoint change notification...");
            
            var env = JsonSerializer.Deserialize<NotificationEnvelope>(msg, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            
            _log.LogInformation("Processing {count} change notifications", env.Value.Length);
            
            foreach (var ch in env.Value) 
            {
                _log.LogDebug("Processing change notification for resource: {resource}", ch.Resource);
                await _handler.HandleAsync(ch);
            }
            
            _log.LogInformation("Successfully processed all change notifications");
        }
        catch (Exception ex)
        {
            // 💾 Save failed message for analysis and potential retry
            string blob = $"processing-error-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.json";
            await _failed.UploadBlobAsync(blob, BinaryData.FromString(msg));
            _log.LogError(ex, "Unhandled error during message processing - saved to {blob}", blob);
            
            // Re-throw to trigger Service Bus retry logic
            throw;
        }
    }
}