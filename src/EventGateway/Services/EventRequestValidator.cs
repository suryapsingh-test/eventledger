using EventLedger.Contracts.Events;

namespace EventGateway.Services;

public static class EventRequestValidator
{
    private static readonly HashSet<string> ValidTypes = new(StringComparer.Ordinal)
    {
        "CREDIT",
        "DEBIT"
    };

    public static IReadOnlyList<string> Validate(EventRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.EventId))
            errors.Add("eventId is required");
        else if (request.EventId.Length > 128)
            errors.Add("eventId must be at most 128 characters");

        if (string.IsNullOrWhiteSpace(request.AccountId))
            errors.Add("accountId is required");
        else if (request.AccountId.Length > 64)
            errors.Add("accountId must be at most 64 characters");

        if (string.IsNullOrWhiteSpace(request.Type))
            errors.Add("type is required");
        else if (!ValidTypes.Contains(request.Type))
            errors.Add("type must be CREDIT or DEBIT");

        if (request.Amount <= 0)
            errors.Add("amount must be greater than 0");
        else if (decimal.Round(request.Amount, 2) != request.Amount)
            errors.Add("amount must have at most 2 decimal places");

        if (string.IsNullOrWhiteSpace(request.Currency))
            errors.Add("currency is required");
        else if (request.Currency.Length > 8)
            errors.Add("currency must be at most 8 characters");

        if (string.IsNullOrWhiteSpace(request.EventTimestamp))
            errors.Add("eventTimestamp is required");
        else if (!DateTimeOffset.TryParse(request.EventTimestamp, out _))
            errors.Add("eventTimestamp must be a valid ISO 8601 date/time");

        return errors;
    }
}
