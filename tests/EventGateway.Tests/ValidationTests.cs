using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EventLedger.Contracts.Events;
using Microsoft.AspNetCore.Mvc;

namespace EventGateway.Tests;

/// <summary>AC-04, AC-05, AC-06 — POST /events validation.</summary>
public sealed class ValidationTests(CountingAccountWebApplicationFactory factory)
    : IClassFixture<CountingAccountWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private readonly CountingAccountWebApplicationFactory _factory = factory;

    [Fact]
    public async Task PostEvent_MissingEventId_Returns400WithMeaningfulError()
    {
        _factory.AccountClient.ResetCallCount();

        var eventId = $"evt-missing-id-{Guid.NewGuid():N}";
        const string json = """
            {
              "eventId": "",
              "accountId": "acct-validation-1",
              "type": "CREDIT",
              "amount": 10.00,
              "currency": "USD",
              "eventTimestamp": "2026-05-15T14:02:11Z"
            }
            """;

        var callsBefore = _factory.AccountClient.ApplyCallCount;
        var response = await _client.PostAsync(
            "/events",
            new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Validation failed", problem.Title);
        Assert.Contains("eventId", problem.Detail ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(callsBefore, _factory.AccountClient.ApplyCallCount);

        var getResponse = await _client.GetAsync($"/events/{eventId}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task PostEvent_EmptyEventId_Returns400AndDoesNotCallAccountService()
    {
        _factory.AccountClient.ResetCallCount();

        var request = GatewayTestHelpers.CreateValidEventRequest(
            string.Empty,
            "acct-validation-2");

        var callsBefore = _factory.AccountClient.ApplyCallCount;
        var response = await _client.PostAsJsonAsync("/events", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("eventId", problem.Detail ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(callsBefore, _factory.AccountClient.ApplyCallCount);
    }

    [Theory]
    [InlineData("TRANSFER")]
    [InlineData("PAYMENT")]
    [InlineData("credit")]
    public async Task PostEvent_InvalidType_Returns400WithMeaningfulError(string invalidType)
    {
        _factory.AccountClient.ResetCallCount();

        var eventId = $"evt-bad-type-{Guid.NewGuid():N}";
        var request = GatewayTestHelpers.CreateValidEventRequest(eventId, "acct-validation-3", type: invalidType);

        var callsBefore = _factory.AccountClient.ApplyCallCount;
        var response = await _client.PostAsJsonAsync("/events", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Validation failed", problem.Title);
        Assert.Contains("CREDIT or DEBIT", problem.Detail ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(callsBefore, _factory.AccountClient.ApplyCallCount);

        var getResponse = await _client.GetAsync($"/events/{eventId}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task PostEvent_ZeroAmount_Returns400WithMeaningfulError()
    {
        _factory.AccountClient.ResetCallCount();

        var eventId = $"evt-zero-{Guid.NewGuid():N}";
        var request = GatewayTestHelpers.CreateValidEventRequest(eventId, "acct-validation-4", amount: 0m);

        var callsBefore = _factory.AccountClient.ApplyCallCount;
        var response = await _client.PostAsJsonAsync("/events", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("amount", problem.Detail ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(callsBefore, _factory.AccountClient.ApplyCallCount);

        var getResponse = await _client.GetAsync($"/events/{eventId}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task PostEvent_NegativeAmount_Returns400WithMeaningfulError()
    {
        _factory.AccountClient.ResetCallCount();

        var eventId = $"evt-negative-{Guid.NewGuid():N}";
        var request = GatewayTestHelpers.CreateValidEventRequest(eventId, "acct-validation-5", amount: -10m);

        var callsBefore = _factory.AccountClient.ApplyCallCount;
        var response = await _client.PostAsJsonAsync("/events", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("amount", problem.Detail ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(callsBefore, _factory.AccountClient.ApplyCallCount);

        var getResponse = await _client.GetAsync($"/events/{eventId}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }
}
