using System.Net;
using System.Net.Http.Json;
using EventLedger.Contracts.Events;
using EventLedger.IntegrationTests.Infrastructure;

namespace EventLedger.IntegrationTests;

/// <summary>AC-01, AC-02 — Gateway POST /events updates Account Service balance via real HTTP.</summary>
public sealed class EndToEndEventFlowTests(EventLedgerIntegrationFixture fixture)
    : IClassFixture<EventLedgerIntegrationFixture>
{
    private readonly HttpClient _gateway = fixture.GatewayClient;
    private readonly HttpClient _account = fixture.AccountClient;

    [Fact]
    public async Task PostCreditEvent_UpdatesAccountBalance()
    {
        const string accountId = "acct-123";
        const string eventId = "evt-001";
        var request = IntegrationTestHelpers.CreateValidEventRequest(eventId, accountId, amount: 150.00m);

        var response = await IntegrationTestHelpers.PostEventAsync(_gateway, request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<EventResponse>();
        Assert.NotNull(created);
        Assert.Equal(eventId, created.EventId);
        Assert.Equal("Applied", created.Status);

        var getResponse = await _gateway.GetAsync($"/events/{eventId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var balance = await IntegrationTestHelpers.GetBalanceAsync(_account, accountId);
        Assert.Equal(150.00m, balance);
    }

    [Fact]
    public async Task PostDebitEvent_DecreasesAccountBalance()
    {
        var accountId = $"acct-debit-{Guid.NewGuid():N}";
        var credit = IntegrationTestHelpers.CreateValidEventRequest(
            $"evt-credit-{Guid.NewGuid():N}",
            accountId,
            type: "CREDIT",
            amount: 200.00m,
            eventTimestamp: "2026-05-15T10:00:00Z");
        var debit = IntegrationTestHelpers.CreateValidEventRequest(
            $"evt-debit-{Guid.NewGuid():N}",
            accountId,
            type: "DEBIT",
            amount: 75.00m,
            eventTimestamp: "2026-05-15T11:00:00Z");

        var creditResponse = await IntegrationTestHelpers.PostEventAsync(_gateway, credit);
        var debitResponse = await IntegrationTestHelpers.PostEventAsync(_gateway, debit);

        Assert.Equal(HttpStatusCode.Created, creditResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, debitResponse.StatusCode);

        var balance = await IntegrationTestHelpers.GetBalanceAsync(_account, accountId);
        Assert.Equal(125.00m, balance);
    }

    [Fact]
    public async Task PostMultipleEventsSameAccount_ListsAllEventsAndCorrectBalance()
    {
        var accountId = $"acct-multi-{Guid.NewGuid():N}";
        var evt1 = IntegrationTestHelpers.CreateValidEventRequest(
            $"evt-m1-{Guid.NewGuid():N}",
            accountId,
            type: "CREDIT",
            amount: 100.00m,
            eventTimestamp: "2026-05-15T10:00:00Z");
        var evt2 = IntegrationTestHelpers.CreateValidEventRequest(
            $"evt-m2-{Guid.NewGuid():N}",
            accountId,
            type: "CREDIT",
            amount: 50.00m,
            eventTimestamp: "2026-05-15T11:00:00Z");
        var evt3 = IntegrationTestHelpers.CreateValidEventRequest(
            $"evt-m3-{Guid.NewGuid():N}",
            accountId,
            type: "DEBIT",
            amount: 25.00m,
            eventTimestamp: "2026-05-15T12:00:00Z");

        await IntegrationTestHelpers.PostEventAsync(_gateway, evt1);
        await IntegrationTestHelpers.PostEventAsync(_gateway, evt2);
        await IntegrationTestHelpers.PostEventAsync(_gateway, evt3);

        var listResponse = await _gateway.GetAsync($"/events?account={accountId}");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var events = await listResponse.Content.ReadFromJsonAsync<List<EventResponse>>();
        Assert.NotNull(events);
        Assert.Equal(3, events.Count);
        Assert.Equal(
            new[] { evt1.EventTimestamp, evt2.EventTimestamp, evt3.EventTimestamp },
            events.Select(e => e.EventTimestamp).ToArray());

        var balance = await IntegrationTestHelpers.GetBalanceAsync(_account, accountId);
        Assert.Equal(125.00m, balance);
    }
}
