namespace EventLedger.Contracts.Accounts;

public sealed record BalanceResponse(
    string AccountId,
    string Currency,
    decimal Balance,
    string AsOf);
