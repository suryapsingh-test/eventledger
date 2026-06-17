namespace EventLedger.Contracts.Accounts;

public sealed record TransactionResponse(
    string EventId,
    string AccountId,
    string Type,
    decimal Amount,
    string Currency,
    string EventTimestamp,
    string AppliedAt,
    decimal BalanceAfter);
