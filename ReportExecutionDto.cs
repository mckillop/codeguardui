using Newtonsoft.Json;

public class ReportExecutionDto
{
    public required string ClientId { get; set; }
    [JsonProperty("id")]
    public required Guid Id { get; set; }
    public required string Timestamp { get; set; }
    public required string ReportType { get; set; }
    public required string Status { get; set; }
    public string? ComponentId { get; set; }
    public string? MimeType { get; set; }
    public string? BlobId { get; set; }
    public string? ReportUrl { get; set; }
    public string? ErrorMessage { get; set; }
    [JsonProperty("ttl")]
    public int Ttl { get; set; }
}