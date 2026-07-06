using EventLedger.Contracts.Accounts;
using AccountService.Data;
using AccountService.Data.Entities;
using AccountService.Validation;
using Microsoft.EntityFrameworkCore;

namespace AccountService.Services;

public interface ITransactionService
{
    Task<(TransactionResponse Response, bool IsReplay)> ApplyTransactionAsync(
        string accountId,
        TransactionRequest request,
        CancellationToken cancellationToken);
}

public sealed class TransactionService(
    AccountDbContext dbContext,
    ILogger<TransactionService> logger) : ITransactionService
{
    public async Task<(TransactionResponse Response, bool IsReplay)> ApplyTransactionAsync(
        string accountId,
        TransactionRequest request,
        CancellationToken cancellationToken)
    {
        var validationErrors = TransactionRequestValidator.Validate(request);
        if (validationErrors.Count > 0)
        {
            throw new ValidationException(validationErrors);
        }

        var existing = await dbContext.Transactions
            .AsNoTracking()
            .FirstOrDefaultAsync(transaction => transaction.EventId == request.EventId, cancellationToken);

        if (existing is not null)
        {
            var replayBalance = await GetAccountBalanceAsync(existing.AccountId, cancellationToken);
            logger.LogInformation(
                "Idempotent replay for event {EventId} on account {AccountId}",
                request.EventId,
                existing.AccountId);

            return (ToResponse(existing, replayBalance), true);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var account = await dbContext.Accounts
            .FirstOrDefaultAsync(entity => entity.AccountId == accountId, cancellationToken);

        var appliedAt = DateTimeOffset.UtcNow.ToString("O");

        if (account is null)
        {
            account = new Account
            {
                AccountId = accountId,
                CreatedAt = appliedAt,
                Currency = request.Currency,
                Balance = 0
            };
            dbContext.Accounts.Add(account);
        }

        var entity = new Transaction
        {
            EventId = request.EventId,
            AccountId = accountId,
            Type = request.Type,
            Amount = request.Amount,
            Currency = request.Currency,
            EventTimestamp = request.EventTimestamp,
            AppliedAt = appliedAt
        };

        dbContext.Transactions.Add(entity);
        ApplyBalanceDelta(account, request.Type, request.Amount);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueEventIdViolation(ex))
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();

            var replay = await dbContext.Transactions
                .AsNoTracking()
                .FirstAsync(t => t.EventId == request.EventId, cancellationToken);

            var replayBalance = await GetAccountBalanceAsync(replay.AccountId, cancellationToken);
            logger.LogInformation(
                "Concurrent idempotent replay for event {EventId}",
                request.EventId);

            return (ToResponse(replay, replayBalance), true);
        }

        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Applied {Type} transaction {EventId} for account {AccountId} amount {Amount}",
            request.Type,
            request.EventId,
            accountId,
            request.Amount);

        return (ToResponse(entity, account.Balance), false);
    }

    private async Task<decimal> GetAccountBalanceAsync(string accountId, CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.AccountId == accountId, cancellationToken);

        return account?.Balance ?? 0;
    }

    private static void ApplyBalanceDelta(Account account, string type, decimal amount)
    {
        account.Balance = type switch
        {
            "CREDIT" => account.Balance + amount,
            "DEBIT" => account.Balance - amount,
            _ => account.Balance
        };
    }

    private static bool IsUniqueEventIdViolation(DbUpdateException exception)
    {
        var message = exception.InnerException?.Message ?? exception.Message;
        return message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
               || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase);
    }

    private static TransactionResponse ToResponse(Transaction entity, decimal balanceAfter) =>
        new(
            entity.EventId,
            entity.AccountId,
            entity.Type,
            entity.Amount,
            entity.Currency,
            entity.EventTimestamp,
            entity.AppliedAt,
            balanceAfter);
}

public sealed class ValidationException(IReadOnlyList<string> errors) : Exception("Validation failed")
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
