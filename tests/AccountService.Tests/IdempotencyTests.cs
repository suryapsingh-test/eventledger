using System.Net;
using System.Net.Http.Json;
using EventLedger.Contracts.Headers;

namespace AccountService.Tests;

/// <summary>AC-03 — Idempotent transaction by eventId (200 + replay header).</summary>
public sealed class IdempotencyTests(AccountServiceWebApplicationFactory factory) : IClassFixture<AccountServiceWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task PostDuplicateEventId_ReturnsOkWithReplayHeader_AndBalanceUnchanged()
    {
        const string accountId = "acct-ac03-idempotent";
        var request = AccountServiceTestHelper.Credit("evt-ac03-dup", 50.00m, "2026-05-15T12:00:00Z");

        var first = await AccountServiceTestHelper.PostTransactionAsync(_client, accountId, request);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var balanceAfterFirst = await AccountServiceTestHelper.GetBalanceAmountAsync(_client, accountId);
        Assert.Equal(50.00m, balanceAfterFirst);

        var second = await AccountServiceTestHelper.PostTransactionAsync(_client, accountId, request);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.True(second.Headers.TryGetValues(EventLedgerHeaders.IdempotencyReplay, out var values));
        Assert.Contains("true", values);

        var balanceAfterReplay = await AccountServiceTestHelper.GetBalanceAmountAsync(_client, accountId);
        Assert.Equal(50.00m, balanceAfterReplay);
    }
}
