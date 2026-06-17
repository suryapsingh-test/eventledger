using System.Net;
using System.Net.Http.Json;
using EventLedger.Contracts.Accounts;

namespace AccountService.Tests;

/// <summary>AC-02 — DEBIT reduces account balance.</summary>
public sealed class DebitBalanceTests(AccountServiceWebApplicationFactory factory) : IClassFixture<AccountServiceWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task PostDebit_AfterCredit_ReducesBalanceByDebitAmount()
    {
        const string accountId = "acct-ac02-debit";

        var creditResponse = await AccountServiceTestHelper.PostTransactionAsync(
            _client,
            accountId,
            AccountServiceTestHelper.Credit("evt-ac02-credit", 200.00m, "2026-05-15T10:00:00Z"));
        Assert.Equal(HttpStatusCode.Created, creditResponse.StatusCode);

        var debitResponse = await AccountServiceTestHelper.PostTransactionAsync(
            _client,
            accountId,
            AccountServiceTestHelper.Debit("evt-ac02-debit", 75.00m, "2026-05-15T11:00:00Z"));
        Assert.Equal(HttpStatusCode.Created, debitResponse.StatusCode);

        var debitBody = await debitResponse.Content.ReadFromJsonAsync<TransactionResponse>();
        Assert.NotNull(debitBody);
        Assert.Equal(125.00m, debitBody.BalanceAfter);

        var balanceResponse = await _client.GetAsync($"/accounts/{accountId}/balance");
        Assert.Equal(HttpStatusCode.OK, balanceResponse.StatusCode);

        var balance = await balanceResponse.Content.ReadFromJsonAsync<BalanceResponse>();
        Assert.NotNull(balance);
        Assert.Equal(125.00m, balance.Balance);
    }
}
