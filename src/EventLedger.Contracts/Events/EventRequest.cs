namespace EventLedger.Contracts.Events;

public sealed class EventRequest
{
    public required string EventId { get; init; }
    public required string AccountId { get; init; }
    public required string Type { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required string EventTimestamp { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}
