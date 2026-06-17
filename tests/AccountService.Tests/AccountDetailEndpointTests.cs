using System.Net;
using System.Net.Http.Json;
using EventLedger.Contracts.Accounts;

namespace AccountService.Tests;

/// <summary>AC-11 — GET /accounts/{accountId} with recent transactions.</summary>
public sealed class AccountDetailEndpointTests(AccountServiceWebApplicationFactory factory) : IClassFixture<AccountServiceWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetAccount_ReturnsIdentifierBalanceAndRecentTransactionsOrderedByEventTimestampDesc()
    {
        const string accountId = "acct-ac11-detail";

        await AccountServiceTestHelper.PostTransactionAsync(
            _client,
            accountId,
            AccountServiceTestHelper.Credit("evt-ac11-a", 10.00m, "2026-05-15T09:00:00Z"));
        await AccountServiceTestHelper.PostTransactionAsync(
            _client,
            accountId,
            AccountServiceTestHelper.Credit("evt-ac11-b", 20.00m, "2026-05-15T10:00:00Z"));

        var response = await _client.GetAsync($"/accounts/{accountId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var account = await response.Content.ReadFromJsonAsync<AccountDetailResponse>();
        Assert.NotNull(account);
        Assert.Equal(accountId, account.AccountId);
        Assert.Equal(30.00m, account.Balance);
        Assert.Equal("USD", account.Currency);
        Assert.False(string.IsNullOrWhiteSpace(account.CreatedAt));
        Assert.Equal(2, account.RecentTransactions.Count);
        Assert.Equal("evt-ac11-b", account.RecentTransactions[0].EventId);
        Assert.Equal("evt-ac11-a", account.RecentTransactions[1].EventId);
    }

    [Fact]
    public async Task GetAccount_BalanceMatchesBalanceEndpoint()
    {
        const string accountId = "acct-ac11-consistency";

        await AccountServiceTestHelper.PostTransactionAsync(
            _client,
            accountId,
            AccountServiceTestHelper.Credit("evt-ac11-c1", 80.00m, "2026-05-15T09:00:00Z"));
        await AccountServiceTestHelper.PostTransactionAsync(
            _client,
            accountId,
            AccountServiceTestHelper.Debit("evt-ac11-c2", 25.00m, "2026-05-15T10:00:00Z"));

        var detailResponse = await _client.GetAsync($"/accounts/{accountId}");
        var balanceResponse = await _client.GetAsync($"/accounts/{accountId}/balance");

        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, balanceResponse.StatusCode);

        var detail = await detailResponse.Content.ReadFromJsonAsync<AccountDetailResponse>();
        var balance = await balanceResponse.Content.ReadFromJsonAsync<BalanceResponse>();

        Assert.NotNull(detail);
        Assert.NotNull(balance);
        Assert.Equal(balance.Balance, detail.Balance);
        Assert.Equal(55.00m, detail.Balance);
    }
}
