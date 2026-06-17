using System.Diagnostics;
using System.Text.Json;
using EventGateway.Clients;
using EventLedger.Contracts.Accounts;
using EventLedger.Contracts.Events;
using EventGateway.Data;
using Microsoft.EntityFrameworkCore;

namespace EventGateway.Services;

public sealed class EventService(
    GatewayDbContext db,
    IAccountServiceClient accountServiceClient,
    ILogger<EventService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public async Task<(EventResponse Response, int StatusCode, bool IsReplay)> SubmitEventAsync(
        EventRequest request,
        string payloadJson,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = EventRequestValidator.Validate(request);
        if (validationErrors.Count > 0)
            throw new EventValidationException(validationErrors);

        var payloadHash = PayloadHasher.ComputeHash(request);
        var existing = await db.Events.AsNoTracking()
            .FirstOrDefaultAsync(e => e.EventId == request.EventId, cancellationToken);

        if (existing is not null)
        {
            if (!string.Equals(existing.PayloadHash, payloadHash, StringComparison.Ordinal))
            {
                logger.LogWarning(
                    "IdempotencyConflict for event {EventId}: payload hash mismatch",
                    request.EventId);
            }

            return (ToResponse(existing), StatusCodes.Status200OK, IsReplay: true);
        }

        var receivedAt = DateTimeOffset.UtcNow;
        var traceId = Activity.Current?.TraceId.ToString();

        var transactionRequest = new TransactionRequest(
            request.EventId,
            request.Type,
            request.Amount,
            request.Currency,
            request.EventTimestamp);

        var traceParent = Activity.Current?.Id;
        var accountResult = await accountServiceClient.ApplyTransactionAsync(
            request.AccountId,
            transactionRequest,
            traceParent,
            cancellationToken);

        if (!accountResult.Success)
            throw new AccountServiceUnavailableException(accountResult.FailureReason ?? "Account Service unavailable");

        var processedAt = DateTimeOffset.UtcNow;
        var metadataJson = request.Metadata is null
            ? null
            : JsonSerializer.Serialize(request.Metadata, JsonOptions);

        var record = new EventRecord
        {
            EventId = request.EventId,
            AccountId = request.AccountId,
            Type = request.Type,
            Amount = request.Amount,
            Currency = request.Currency,
            EventTimestamp = request.EventTimestamp,
            MetadataJson = metadataJson,
            PayloadJson = payloadJson,
            PayloadHash = payloadHash,
            ReceivedAt = receivedAt.ToString("O"),
            ProcessedAt = processedAt.ToString("O"),
            Status = "Applied",
            TraceId = traceId
        };

        db.Events.Add(record);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueEventIdViolation(ex))
        {
            db.ChangeTracker.Clear();

            var replay = await db.Events.AsNoTracking()
                .FirstAsync(e => e.EventId == request.EventId, cancellationToken);

            if (!string.Equals(replay.PayloadHash, payloadHash, StringComparison.Ordinal))
            {
                logger.LogWarning(
                    "IdempotencyConflict for event {EventId}: payload hash mismatch on concurrent insert",
                    request.EventId);
            }

            logger.LogInformation(
                "Concurrent idempotent replay for event {EventId}",
                request.EventId);

            return (ToResponse(replay), StatusCodes.Status200OK, IsReplay: true);
        }

        logger.LogInformation(
            "Event applied {EventId} for account {AccountId}",
            request.EventId,
            request.AccountId);

        return (ToResponse(record), StatusCodes.Status201Created, IsReplay: false);
    }

    public async Task<EventResponse?> GetEventByIdAsync(string eventId, CancellationToken cancellationToken = default)
    {
        var record = await db.Events.AsNoTracking()
            .FirstOrDefaultAsync(e => e.EventId == eventId, cancellationToken);

        return record is null ? null : ToResponse(record);
    }

    public async Task<IReadOnlyList<EventResponse>> GetEventsByAccountAsync(
        string accountId,
        CancellationToken cancellationToken = default)
    {
        var records = await db.Events.AsNoTracking()
            .Where(e => e.AccountId == accountId)
            .OrderBy(e => e.EventTimestamp)
            .ThenBy(e => e.EventId)
            .ToListAsync(cancellationToken);

        return records.Select(ToResponse).ToList();
    }

    private static bool IsUniqueEventIdViolation(DbUpdateException exception)
    {
        var message = exception.InnerException?.Message ?? exception.Message;
        return message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
               || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase);
    }

    private static EventResponse ToResponse(EventRecord record)
    {
        Dictionary<string, object>? metadata = null;
        if (!string.IsNullOrWhiteSpace(record.MetadataJson))
            metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(record.MetadataJson, JsonOptions);

        return new EventResponse
        {
            EventId = record.EventId,
            AccountId = record.AccountId,
            Type = record.Type,
            Amount = record.Amount,
            Currency = record.Currency,
            EventTimestamp = record.EventTimestamp,
            Metadata = metadata,
            ReceivedAt = record.ReceivedAt,
            Status = record.Status
        };
    }
}

public sealed class EventValidationException(IReadOnlyList<string> errors) : Exception(string.Join("; ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}

public sealed class AccountServiceUnavailableException(string detail) : Exception(detail)
{
    public string Detail { get; } = detail;
}
