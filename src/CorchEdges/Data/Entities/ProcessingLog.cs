namespace CorchEdges.Data.Entities;

public class ProcessingLog
{
    public long Id { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? SharePointItemId { get; set; }
    public string? ExceptionDetails { get; set; }
}