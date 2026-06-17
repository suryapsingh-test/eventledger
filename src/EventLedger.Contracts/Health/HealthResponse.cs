namespace EventLedger.Contracts.Health;

public sealed record HealthResponse(
    string Status,
    string Service,
    IReadOnlyDictionary<string, string> Checks,
    string Timestamp);
