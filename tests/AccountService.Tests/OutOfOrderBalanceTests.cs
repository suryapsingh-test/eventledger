using System.Net;

namespace AccountService.Tests;

/// <summary>AC-08 — Out-of-order submission yields correct balance.</summary>
public sealed class OutOfOrderBalanceTests(AccountServiceWebApplicationFactory factory) : IClassFixture<AccountServiceWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task PostTransactionsInReverseChronologicalOrder_ProducesCorrectFinalBalance()
    {
        const string accountId = "acct-ac08-out-of-order";

        // Chronological order: T1 CREDIT 100, T2 DEBIT 30, T3 CREDIT 50 → balance 120
        var t3 = AccountServiceTestHelper.Credit("evt-ac08-t3", 50.00m, "2026-05-15T12:00:00Z");
        var t2 = AccountServiceTestHelper.Debit("evt-ac08-t2", 30.00m, "2026-05-15T11:00:00Z");
        var t1 = AccountServiceTestHelper.Credit("evt-ac08-t1", 100.00m, "2026-05-15T10:00:00Z");

        var responseT3 = await AccountServiceTestHelper.PostTransactionAsync(_client, accountId, t3);
        var responseT2 = await AccountServiceTestHelper.PostTransactionAsync(_client, accountId, t2);
        var responseT1 = await AccountServiceTestHelper.PostTransactionAsync(_client, accountId, t1);

        Assert.Equal(HttpStatusCode.Created, responseT3.StatusCode);
        Assert.Equal(HttpStatusCode.Created, responseT2.StatusCode);
        Assert.Equal(HttpStatusCode.Created, responseT1.StatusCode);

        var balance = await AccountServiceTestHelper.GetBalanceAmountAsync(_client, accountId);
        Assert.Equal(120.00m, balance);
    }
}
