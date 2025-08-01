public class ReportExecution
{
    public required string clientId { get; set; }
    public required Guid id { get; set; }
    public required string timestamp { get; set; }
    public required string reportType { get; set; }
    public required string status { get; set; }
    public string? componentId { get; set; }
    public string? mimeType { get; set; }
    public string? blobId { get; set; }
    public string? reportUrl { get; set; }
    public string? errorMessage { get; set; }
    public int ttl { get; set; }
}