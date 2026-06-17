using EventLedger.Contracts.Accounts;
using AccountService.Data;
using Microsoft.EntityFrameworkCore;

namespace AccountService.Services;

public interface IAccountQueryService
{
    Task<BalanceResponse?> GetBalanceAsync(string accountId, CancellationToken cancellationToken);

    Task<AccountDetailResponse?> GetAccountDetailAsync(string accountId, CancellationToken cancellationToken);
}

public sealed class AccountQueryService(AccountDbContext dbContext) : IAccountQueryService
{
    public async Task<BalanceResponse?> GetBalanceAsync(string accountId, CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.AccountId == accountId, cancellationToken);

        if (account is null)
        {
            return null;
        }

        var balance = await BalanceCalculator.ComputeBalanceAsync(dbContext, accountId, cancellationToken);

        return new BalanceResponse(
            account.AccountId,
            account.Currency,
            balance,
            DateTimeOffset.UtcNow.ToString("O"));
    }

    public async Task<AccountDetailResponse?> GetAccountDetailAsync(string accountId, CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.AccountId == accountId, cancellationToken);

        if (account is null)
        {
            return null;
        }

        var balance = await BalanceCalculator.ComputeBalanceAsync(dbContext, accountId, cancellationToken);

        var recentTransactions = await dbContext.Transactions
            .AsNoTracking()
            .Where(transaction => transaction.AccountId == accountId)
            .OrderByDescending(transaction => transaction.EventTimestamp)
            .ThenByDescending(transaction => transaction.EventId)
            .Take(20)
            .Select(transaction => new AccountTransactionSummary(
                transaction.EventId,
                transaction.Type,
                transaction.Amount,
                transaction.EventTimestamp,
                transaction.AppliedAt))
            .ToListAsync(cancellationToken);

        return new AccountDetailResponse(
            account.AccountId,
            account.CreatedAt,
            balance,
            account.Currency,
            recentTransactions);
    }
}
