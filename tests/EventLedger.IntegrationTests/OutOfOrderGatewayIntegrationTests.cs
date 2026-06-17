using System.Net;
using EventLedger.IntegrationTests.Infrastructure;

namespace EventLedger.IntegrationTests;

/// <summary>AC-08 — Out-of-order arrival via Gateway yields correct Account balance.</summary>
public sealed class OutOfOrderGatewayIntegrationTests(EventLedgerIntegrationFixture fixture)
    : IClassFixture<EventLedgerIntegrationFixture>
{
    private readonly HttpClient _gateway = fixture.GatewayClient;
    private readonly HttpClient _account = fixture.AccountClient;

    [Fact]
    public async Task PostEventsInReverseChronologicalOrder_ProducesCorrectFinalBalance()
    {
        const string accountId = "acct-ac08-integration";

        // Chronological: T1 CREDIT 100, T2 DEBIT 30, T3 CREDIT 50 → balance 120
        var t3 = IntegrationTestHelpers.CreateValidEventRequest(
            "evt-ac08-t3",
            accountId,
            type: "CREDIT",
            amount: 50.00m,
            eventTimestamp: "2026-05-15T12:00:00Z");
        var t2 = IntegrationTestHelpers.CreateValidEventRequest(
            "evt-ac08-t2",
            accountId,
            type: "DEBIT",
            amount: 30.00m,
            eventTimestamp: "2026-05-15T11:00:00Z");
        var t1 = IntegrationTestHelpers.CreateValidEventRequest(
            "evt-ac08-t1",
            accountId,
            type: "CREDIT",
            amount: 100.00m,
            eventTimestamp: "2026-05-15T10:00:00Z");

        var responseT3 = await IntegrationTestHelpers.PostEventAsync(_gateway, t3);
        var responseT2 = await IntegrationTestHelpers.PostEventAsync(_gateway, t2);
        var responseT1 = await IntegrationTestHelpers.PostEventAsync(_gateway, t1);

        Assert.Equal(HttpStatusCode.Created, responseT3.StatusCode);
        Assert.Equal(HttpStatusCode.Created, responseT2.StatusCode);
        Assert.Equal(HttpStatusCode.Created, responseT1.StatusCode);

        var balance = await IntegrationTestHelpers.GetBalanceAsync(_account, accountId);
        Assert.Equal(120.00m, balance);
    }
}
