using System.Net;
using System.Net.Http.Json;
using EventLedger.Contracts.Events;
using EventLedger.IntegrationTests.Infrastructure;

namespace EventLedger.IntegrationTests;

/// <summary>AC-03 — Duplicate eventId via Gateway does not double-apply balance.</summary>
public sealed class IdempotencyIntegrationTests(EventLedgerIntegrationFixture fixture)
    : IClassFixture<EventLedgerIntegrationFixture>
{
    private readonly HttpClient _gateway = fixture.GatewayClient;
    private readonly HttpClient _account = fixture.AccountClient;

    [Fact]
    public async Task PostDuplicateEventId_BalanceUnchangedAfterReplay()
    {
        var accountId = $"acct-idem-integ-{Guid.NewGuid():N}";
        var eventId = $"evt-dup-integ-{Guid.NewGuid():N}";
        var request = IntegrationTestHelpers.CreateValidEventRequest(eventId, accountId, amount: 80.00m);

        var first = await IntegrationTestHelpers.PostEventAsync(_gateway, request);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var balanceAfterFirst = await IntegrationTestHelpers.GetBalanceAsync(_account, accountId);
        Assert.Equal(80.00m, balanceAfterFirst);

        var second = await IntegrationTestHelpers.PostEventAsync(_gateway, request);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.True(second.Headers.TryGetValues("Idempotency-Replay", out var replayValues));
        Assert.Contains("true", replayValues, StringComparer.OrdinalIgnoreCase);

        var replayed = await second.Content.ReadFromJsonAsync<EventResponse>();
        Assert.NotNull(replayed);
        Assert.Equal(eventId, replayed.EventId);

        var balanceAfterReplay = await IntegrationTestHelpers.GetBalanceAsync(_account, accountId);
        Assert.Equal(80.00m, balanceAfterReplay);

        var listResponse = await _gateway.GetAsync($"/events?account={accountId}");
        var events = await listResponse.Content.ReadFromJsonAsync<List<EventResponse>>();
        Assert.NotNull(events);
        Assert.Single(events);
    }
}
