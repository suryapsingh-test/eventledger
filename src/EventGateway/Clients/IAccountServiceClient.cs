using EventLedger.Contracts.Accounts;

namespace EventGateway.Clients;

public sealed record AccountTransactionResult(bool Success, bool IsReplay, string? FailureReason = null);

public interface IAccountServiceClient
{
    Task<AccountTransactionResult> ApplyTransactionAsync(
        string accountId,
        TransactionRequest request,
        string? traceParent,
        CancellationToken cancellationToken = default);
}
