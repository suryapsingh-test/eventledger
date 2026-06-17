using EventLedger.Contracts.Events;

namespace EventGateway.Tests;

internal static class GatewayTestHelpers
{
    public static EventRequest CreateValidEventRequest(
        string eventId,
        string accountId,
        string eventTimestamp = "2026-05-15T14:02:11Z",
        string type = "CREDIT",
        decimal amount = 150.00m) =>
        new()
        {
            EventId = eventId,
            AccountId = accountId,
            Type = type,
            Amount = amount,
            Currency = "USD",
            EventTimestamp = eventTimestamp,
            Metadata = new Dictionary<string, object> { ["source"] = "gateway-unit-test" }
        };
}
