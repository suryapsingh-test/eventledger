namespace EventLedger.Contracts.Accounts;

public sealed record AccountTransactionSummary(
    string EventId,
    string Type,
    decimal Amount,
    string EventTimestamp,
    string AppliedAt);

public sealed record AccountDetailResponse(
    string AccountId,
    string CreatedAt,
    decimal Balance,
    string Currency,
    IReadOnlyList<AccountTransactionSummary> RecentTransactions);
