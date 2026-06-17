using System.Net;
using System.Net.Http.Json;
using EventLedger.Contracts.Events;
using Microsoft.AspNetCore.Mvc;

namespace EventGateway.Tests;

/// <summary>AC-07, AC-09 — GET /events and GET /events/{id}.</summary>
public sealed class EventQueryTests(EventGatewayWebApplicationFactory factory)
    : IClassFixture<EventGatewayWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetEventById_WhenExists_Returns200WithFullPayload()
    {
        var eventId = $"evt-get-{Guid.NewGuid():N}";
        var request = GatewayTestHelpers.CreateValidEventRequest(eventId, "acct-query-1");

        var postResponse = await _client.PostAsJsonAsync("/events", request);
        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);

        var getResponse = await _client.GetAsync($"/events/{eventId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var fetched = await getResponse.Content.ReadFromJsonAsync<EventResponse>();
        Assert.NotNull(fetched);
        Assert.Equal(eventId, fetched.EventId);
        Assert.Equal(request.AccountId, fetched.AccountId);
        Assert.Equal(request.Type, fetched.Type);
        Assert.Equal(request.Amount, fetched.Amount);
        Assert.Equal(request.Currency, fetched.Currency);
        Assert.Equal(request.EventTimestamp, fetched.EventTimestamp);
        Assert.Equal("Applied", fetched.Status);
        Assert.False(string.IsNullOrWhiteSpace(fetched.ReceivedAt));
    }

    [Fact]
    public async Task GetEventById_WhenNotFound_Returns404()
    {
        var response = await _client.GetAsync("/events/nonexistent-event-id");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Event not found", problem.Title);
        Assert.Contains("nonexistent-event-id", problem.Detail ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetEventsByAccount_ReturnsOrderedByEventTimestampAscending()
    {
        var accountId = $"acct-order-{Guid.NewGuid():N}";

        var evtT1 = GatewayTestHelpers.CreateValidEventRequest(
            $"evt-t1-{Guid.NewGuid():N}",
            accountId,
            eventTimestamp: "2026-05-15T10:00:00Z");
        var evtT3 = GatewayTestHelpers.CreateValidEventRequest(
            $"evt-t3-{Guid.NewGuid():N}",
            accountId,
            eventTimestamp: "2026-05-15T14:00:00Z");
        var evtT2 = GatewayTestHelpers.CreateValidEventRequest(
            $"evt-t2-{Guid.NewGuid():N}",
            accountId,
            eventTimestamp: "2026-05-15T12:00:00Z");

        await _client.PostAsJsonAsync("/events", evtT3);
        await _client.PostAsJsonAsync("/events", evtT1);
        await _client.PostAsJsonAsync("/events", evtT2);

        var response = await _client.GetAsync($"/events?account={accountId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var events = await response.Content.ReadFromJsonAsync<List<EventResponse>>();
        Assert.NotNull(events);
        Assert.Equal(3, events.Count);
        Assert.Equal(evtT1.EventId, events[0].EventId);
        Assert.Equal(evtT2.EventId, events[1].EventId);
        Assert.Equal(evtT3.EventId, events[2].EventId);
        Assert.Equal(
            events.Select(e => e.EventTimestamp).OrderBy(t => t).ToList(),
            events.Select(e => e.EventTimestamp).ToList());
    }

    [Fact]
    public async Task GetEventsByAccount_SameTimestamp_OrdersByEventId()
    {
        var accountId = $"acct-tie-{Guid.NewGuid():N}";
        const string timestamp = "2026-05-15T12:00:00Z";

        var evtB = GatewayTestHelpers.CreateValidEventRequest($"evt-b-{Guid.NewGuid():N}", accountId, timestamp);
        var evtA = GatewayTestHelpers.CreateValidEventRequest($"evt-a-{Guid.NewGuid():N}", accountId, timestamp);
        var evtC = GatewayTestHelpers.CreateValidEventRequest($"evt-c-{Guid.NewGuid():N}", accountId, timestamp);

        await _client.PostAsJsonAsync("/events", evtB);
        await _client.PostAsJsonAsync("/events", evtA);
        await _client.PostAsJsonAsync("/events", evtC);

        var response = await _client.GetAsync($"/events?account={accountId}");
        var events = await response.Content.ReadFromJsonAsync<List<EventResponse>>();

        Assert.NotNull(events);
        Assert.Equal(3, events.Count);
        Assert.Equal(evtA.EventId, events[0].EventId);
        Assert.Equal(evtB.EventId, events[1].EventId);
        Assert.Equal(evtC.EventId, events[2].EventId);
    }

    [Fact]
    public async Task GetEvents_MissingAccountParam_Returns400()
    {
        var response = await _client.GetAsync("/events");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Validation failed", problem.Title);
        Assert.Contains("account", problem.Detail ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
