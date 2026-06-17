namespace EventLedger.Contracts.Accounts;

public sealed record TransactionRequest(
    string EventId,
    string Type,
    decimal Amount,
    string Currency,
    string EventTimestamp);
