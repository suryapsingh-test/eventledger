using System.Net;
using System.Net.Http.Json;
using EventLedger.Contracts.Events;

namespace EventGateway.Tests;

/// <summary>AC-03 — duplicate eventId handling.</summary>
public sealed class IdempotencyTests(CountingAccountWebApplicationFactory factory)
    : IClassFixture<CountingAccountWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private readonly CountingAccountWebApplicationFactory _factory = factory;

    [Fact]
    public async Task PostEvent_DuplicateEventId_ReturnsOriginalEventWithoutSecondAccountCall()
    {
        _factory.AccountClient.ResetCallCount();

        var eventId = $"evt-dup-{Guid.NewGuid():N}";
        var accountId = $"acct-idem-{Guid.NewGuid():N}";
        var request = GatewayTestHelpers.CreateValidEventRequest(eventId, accountId);

        var first = await _client.PostAsJsonAsync("/events", request);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.False(first.Headers.Contains("Idempotency-Replay"));

        var created = await first.Content.ReadFromJsonAsync<EventResponse>();
        Assert.NotNull(created);
        Assert.Equal("Applied", created.Status);

        var callsAfterFirst = _factory.AccountClient.ApplyCallCount;
        Assert.Equal(1, callsAfterFirst);

        var second = await _client.PostAsJsonAsync("/events", request);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.True(second.Headers.TryGetValues("Idempotency-Replay", out var replayValues));
        Assert.Contains("true", replayValues, StringComparer.OrdinalIgnoreCase);

        var replayed = await second.Content.ReadFromJsonAsync<EventResponse>();
        Assert.NotNull(replayed);
        Assert.Equal(created.EventId, replayed.EventId);
        Assert.Equal(created.AccountId, replayed.AccountId);
        Assert.Equal(created.Type, replayed.Type);
        Assert.Equal(created.Amount, replayed.Amount);
        Assert.Equal(created.Status, replayed.Status);

        Assert.Equal(callsAfterFirst, _factory.AccountClient.ApplyCallCount);

        var listResponse = await _client.GetAsync($"/events?account={accountId}");
        var events = await listResponse.Content.ReadFromJsonAsync<List<EventResponse>>();
        Assert.NotNull(events);
        Assert.Single(events);
        Assert.Equal(eventId, events[0].EventId);
    }

    [Fact]
    public async Task PostEvent_ConcurrentDuplicateEventId_BothReturnSuccess()
    {
        _factory.AccountClient.ResetCallCount();

        var eventId = $"evt-concurrent-{Guid.NewGuid():N}";
        var accountId = $"acct-concurrent-{Guid.NewGuid():N}";
        var request = GatewayTestHelpers.CreateValidEventRequest(eventId, accountId);

        var tasks = Enumerable.Range(0, 2)
            .Select(_ => _client.PostAsJsonAsync("/events", request))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        Assert.All(responses, r =>
            Assert.True(
                r.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK,
                $"Expected 201 or 200, got {r.StatusCode}"));

        var statusCodes = responses.Select(r => r.StatusCode).ToList();
        Assert.Contains(HttpStatusCode.Created, statusCodes);
        Assert.Contains(HttpStatusCode.OK, statusCodes);

        var listResponse = await _client.GetAsync($"/events?account={accountId}");
        var events = await listResponse.Content.ReadFromJsonAsync<List<EventResponse>>();
        Assert.NotNull(events);
        Assert.Single(events);
    }
}
