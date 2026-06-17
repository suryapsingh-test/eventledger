namespace EventGateway.Data;

public sealed class EventRecord
{
    public required string EventId { get; set; }
    public required string AccountId { get; set; }
    public required string Type { get; set; }
    public decimal Amount { get; set; }
    public required string Currency { get; set; }
    public required string EventTimestamp { get; set; }
    public string? MetadataJson { get; set; }
    public required string PayloadJson { get; set; }
    public required string PayloadHash { get; set; }
    public required string ReceivedAt { get; set; }
    public required string ProcessedAt { get; set; }
    public required string Status { get; set; }
    public string? TraceId { get; set; }
}
