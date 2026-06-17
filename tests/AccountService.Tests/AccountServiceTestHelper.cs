using System.Net;
using System.Net.Http.Json;
using EventLedger.Contracts.Accounts;

namespace AccountService.Tests;

internal static class AccountServiceTestHelper
{
    public static async Task<HttpResponseMessage> PostTransactionAsync(
        HttpClient client,
        string accountId,
        TransactionRequest request)
    {
        return await client.PostAsJsonAsync($"/accounts/{accountId}/transactions", request);
    }

    public static async Task<decimal?> GetBalanceAmountAsync(HttpClient client, string accountId)
    {
        var response = await client.GetAsync($"/accounts/{accountId}/balance");
        if (response.StatusCode != HttpStatusCode.OK)
        {
            return null;
        }

        var balance = await response.Content.ReadFromJsonAsync<BalanceResponse>();
        return balance?.Balance;
    }

    public static TransactionRequest Credit(string eventId, decimal amount, string timestamp) =>
        new(eventId, "CREDIT", amount, "USD", timestamp);

    public static TransactionRequest Debit(string eventId, decimal amount, string timestamp) =>
        new(eventId, "DEBIT", amount, "USD", timestamp);
}
