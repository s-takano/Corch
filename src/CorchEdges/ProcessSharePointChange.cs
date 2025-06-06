using System.Data;
using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Azure.Storage.Blobs;
using CorchEdges.Data;
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
//  Abstractions for unit tests
// ─────────────────────────────────────────────────────────────────────────────
public interface IGraphFacade
{
    Task<ListItem?> GetListItemAsync(string siteId, string listId, string itemId);
    Task<DriveItem?> GetDriveItemAsync(string siteId, string listId, string itemId);
    Task<Stream> DownloadAsync(string driveId, string driveItemId);
}

public interface IExcelParser { (DataSet?, string?) Parse(byte[] bytes); }
public interface IDatabaseWriter
{
    Task WriteAsync(DataSet tables, EdgesDbContext context, DbConnection connection, DbTransaction transaction);
}

// Default production implementations ------------------------------------------------

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
    { _log = log; _handler = handler; _failed = blobs.GetBlobContainerClient("failed-changes"); }

    [Function(nameof(ProcessSharePointChange))]
    public async Task RunAsync([ServiceBusTrigger("sp-changes", Connection="ServiceBusConnection")] string msg)
    {
        try
        {
            var env = JsonSerializer.Deserialize<NotificationEnvelope>(msg, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            foreach (var ch in env.Value) await _handler.HandleAsync(ch);
        }
        catch (Exception ex)
        {
            string blob = $"msg-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.json";
            await _failed.UploadBlobAsync(blob, BinaryData.FromString(msg));
            _log.LogError(ex, "Unhandled error saved to {blob}", blob);
            throw;
        }
    }
}