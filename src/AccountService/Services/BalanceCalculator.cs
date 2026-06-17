using AccountService.Data;
using AccountService.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AccountService.Services;

public static class BalanceCalculator
{
    public static async Task<decimal> ComputeBalanceAsync(
        AccountDbContext dbContext,
        string accountId,
        CancellationToken cancellationToken)
    {
        var transactions = await dbContext.Transactions
            .AsNoTracking()
            .Where(transaction => transaction.AccountId == accountId)
            .Select(transaction => new { transaction.Type, transaction.Amount })
            .ToListAsync(cancellationToken);

        decimal credits = 0;
        decimal debits = 0;

        foreach (var transaction in transactions)
        {
            if (transaction.Type == "CREDIT")
            {
                credits += transaction.Amount;
            }
            else if (transaction.Type == "DEBIT")
            {
                debits += transaction.Amount;
            }
        }

        return credits - debits;
    }
}
