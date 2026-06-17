using System.Net;
using System.Net.Http.Json;
using EventLedger.Contracts.Events;
using Microsoft.AspNetCore.Mvc;

namespace EventGateway.Tests;

/// <summary>AC-13, AC-14 — behavior when Account Service is unavailable.</summary>
public sealed class DegradationTests : IDisposable
{
    private readonly string _sharedDbPath = Path.Combine(
        Path.GetTempPath(),
        $"gateway-degrade-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        if (File.Exists(_sharedDbPath))
        {
            try { File.Delete(_sharedDbPath); } catch { /* best effort */ }
        }
    }

    [Fact]
    public async Task PostEvent_AccountUnavailable_Returns503AndDoesNotPersist()
    {
        using var factory = new FailingAccountWebApplicationFactory();
        var client = factory.CreateClient();

        var eventId = $"evt-fail-{Guid.NewGuid():N}";
        var request = GatewayTestHelpers.CreateValidEventRequest(eventId, "acct-fail-1");

        var postResponse = await client.PostAsJsonAsync("/events", request);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, postResponse.StatusCode);

        var problem = await postResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Account Service unavailable", problem.Title);
        Assert.False(string.IsNullOrWhiteSpace(problem.Detail));

        var getResponse = await client.GetAsync($"/events/{eventId}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task GetEventById_PersistedEvents_AccountUnavailable_StillReturnsStoredEvent()
    {
        var eventId = $"evt-persist-{Guid.NewGuid():N}";
        var accountId = $"acct-persist-{Guid.NewGuid():N}";
        var request = GatewayTestHelpers.CreateValidEventRequest(eventId, accountId);

        using (var seedFactory = new SharedDatabaseEventGatewayWebApplicationFactory(_sharedDbPath))
        {
            var seedClient = seedFactory.CreateClient();
            var postResponse = await seedClient.PostAsJsonAsync("/events", request);
            Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);
        }

        using var failingFactory = new FailingAccountWebApplicationFactory(_sharedDbPath);
        var client = failingFactory.CreateClient();

        var getResponse = await client.GetAsync($"/events/{eventId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var fetched = await getResponse.Content.ReadFromJsonAsync<EventResponse>();
        Assert.NotNull(fetched);
        Assert.Equal(eventId, fetched.EventId);
        Assert.Equal(accountId, fetched.AccountId);
    }

    [Fact]
    public async Task GetEventsByAccount_PersistedEvents_AccountUnavailable_StillReturnsStoredEvents()
    {
        var accountId = $"acct-list-{Guid.NewGuid():N}";
        var evt1 = GatewayTestHelpers.CreateValidEventRequest(
            $"evt-list-1-{Guid.NewGuid():N}",
            accountId,
            eventTimestamp: "2026-05-15T10:00:00Z");
        var evt2 = GatewayTestHelpers.CreateValidEventRequest(
            $"evt-list-2-{Guid.NewGuid():N}",
            accountId,
            eventTimestamp: "2026-05-15T12:00:00Z");

        using (var seedFactory = new SharedDatabaseEventGatewayWebApplicationFactory(_sharedDbPath))
        {
            var seedClient = seedFactory.CreateClient();
            await seedClient.PostAsJsonAsync("/events", evt2);
            await seedClient.PostAsJsonAsync("/events", evt1);
        }

        using var failingFactory = new FailingAccountWebApplicationFactory(_sharedDbPath);
        var client = failingFactory.CreateClient();

        var response = await client.GetAsync($"/events?account={accountId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var events = await response.Content.ReadFromJsonAsync<List<EventResponse>>();
        Assert.NotNull(events);
        Assert.Equal(2, events.Count);
        Assert.Equal(evt1.EventId, events[0].EventId);
        Assert.Equal(evt2.EventId, events[1].EventId);
    }
}
