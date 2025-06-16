namespace CorchEdges.Data.Entities;

public class ProcessedFile
{
    public long Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? SharePointItemId { get; set; }
    public DateTime ProcessedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public int RecordCount { get; set; }
}