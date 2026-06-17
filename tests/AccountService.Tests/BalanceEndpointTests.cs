using System.Net;
using System.Net.Http.Json;
using EventLedger.Contracts.Accounts;

namespace AccountService.Tests;

/// <summary>AC-10 — GET /accounts/{accountId}/balance.</summary>
public sealed class BalanceEndpointTests(AccountServiceWebApplicationFactory factory) : IClassFixture<AccountServiceWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetBalance_ReturnsSumOfCreditsMinusDebits()
    {
        const string accountId = "acct-ac10-balance";

        await AccountServiceTestHelper.PostTransactionAsync(
            _client,
            accountId,
            AccountServiceTestHelper.Credit("evt-ac10-1", 100.00m, "2026-05-15T09:00:00Z"));
        await AccountServiceTestHelper.PostTransactionAsync(
            _client,
            accountId,
            AccountServiceTestHelper.Debit("evt-ac10-2", 30.00m, "2026-05-15T10:00:00Z"));
        await AccountServiceTestHelper.PostTransactionAsync(
            _client,
            accountId,
            AccountServiceTestHelper.Credit("evt-ac10-3", 50.00m, "2026-05-15T11:00:00Z"));

        var response = await _client.GetAsync($"/accounts/{accountId}/balance");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var balance = await response.Content.ReadFromJsonAsync<BalanceResponse>();
        Assert.NotNull(balance);
        Assert.Equal(accountId, balance.AccountId);
        Assert.Equal("USD", balance.Currency);
        Assert.Equal(120.00m, balance.Balance);
        Assert.False(string.IsNullOrWhiteSpace(balance.AsOf));
    }

    [Fact]
    public async Task GetBalance_ForUnknownAccount_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/accounts/unknown-account-ac10/balance");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
