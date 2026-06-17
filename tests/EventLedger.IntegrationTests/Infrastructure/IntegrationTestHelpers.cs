using System.Net.Http.Json;
using EventLedger.Contracts.Accounts;
using EventLedger.Contracts.Events;

namespace EventLedger.IntegrationTests.Infrastructure;

internal static class IntegrationTestHelpers
{
    public static EventRequest CreateValidEventRequest(
        string eventId,
        string accountId,
        string eventTimestamp = "2026-05-15T14:02:11Z",
        string type = "CREDIT",
        decimal amount = 150.00m) =>
        CreateEvent(eventId, accountId, eventTimestamp, type, amount);

    public static EventRequest CreateEvent(
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
            Metadata = new Dictionary<string, object> { ["source"] = "integration-test" }
        };

    public static async Task<decimal?> GetBalanceAsync(HttpClient accountClient, string accountId)
    {
        var response = await accountClient.GetAsync($"/accounts/{accountId}/balance");
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var balance = await response.Content.ReadFromJsonAsync<BalanceResponse>();
        return balance?.Balance;
    }

    public static async Task<HttpResponseMessage> PostEventAsync(
        HttpClient gatewayClient,
        EventRequest request) =>
        await gatewayClient.PostAsJsonAsync("/events", request);

    public static string? ExtractTraceIdFromTraceParent(string? traceParent)
    {
        if (string.IsNullOrWhiteSpace(traceParent))
            return null;

        var parts = traceParent.Split('-');
        return parts.Length >= 2 ? parts[1] : null;
    }
}
