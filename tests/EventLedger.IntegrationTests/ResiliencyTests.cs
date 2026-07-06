using System.Net;
using System.Net.Http.Json;
using EventGateway.Resilience;
using EventLedger.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace EventLedger.IntegrationTests;

/// <summary>AC-13, AC-18 — resiliency through real HttpClient + Polly pipeline.</summary>
public sealed class ResiliencyTests
{
    [Fact]
    public async Task PostEvent_AccountReturns500_RetriesThenReturns503AndDoesNotPersist()
    {
        using var factory = new CapturingGatewayWebApplicationFactory();
        factory.AccountHandler.ResponseStatusCode = HttpStatusCode.InternalServerError;
        using var client = factory.CreateClient();

        var eventId = $"evt-500-{Guid.NewGuid():N}";
        var request = IntegrationTestHelpers.CreateEvent(eventId, "acct-resilience-500");

        var postResponse = await client.PostAsJsonAsync("/events", request);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, postResponse.StatusCode);

        var problem = await postResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Account Service unavailable", problem.Title);
        Assert.False(string.IsNullOrWhiteSpace(problem.Detail));

        var expectedAttempts = 1 + new AccountServiceResilienceOptions().MaxRetryAttempts;
        Assert.Equal(expectedAttempts, factory.AccountHandler.RequestCount);

        var getResponse = await client.GetAsync($"/events/{eventId}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task PostEvent_TransientFailuresThenSuccess_RetriesAndPersistsEvent()
    {
        using var factory = new CapturingGatewayWebApplicationFactory();
        factory.AccountHandler.ResponseStatusCode = HttpStatusCode.Created;
        factory.AccountHandler.RemainingTransientFailures = 2;
        factory.AccountHandler.TransientFailureStatusCode = HttpStatusCode.ServiceUnavailable;

        using var client = factory.CreateClient();

        var eventId = $"evt-retry-{Guid.NewGuid():N}";
        var request = IntegrationTestHelpers.CreateEvent(eventId, "acct-retry-success");

        var postResponse = await client.PostAsJsonAsync("/events", request);
        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);
        Assert.Equal(3, factory.AccountHandler.RequestCount);

        var getResponse = await client.GetAsync($"/events/{eventId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task PostEvent_AccountUnreachable_Returns503WithinTimeout()
    {
        using var factory = new CapturingGatewayWebApplicationFactory(useCapturingHandler: false);
        factory.WithResilienceOptions(new AccountServiceResilienceOptions { MaxRetryAttempts = 0 });
        using var client = factory.CreateClient();

        var request = IntegrationTestHelpers.CreateEvent(
            $"evt-down-{Guid.NewGuid():N}",
            "acct-unreachable");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var response = await client.PostAsJsonAsync("/events", request);
        stopwatch.Stop();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), "Request should not hang indefinitely.");

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Account Service unavailable", problem.Title);
        Assert.False(string.IsNullOrWhiteSpace(problem.Detail));
    }

    [Fact]
    public async Task PostEvent_RepeatedAccountFailures_OpensCircuitBreakerAndFailsFast()
    {
        using var factory = new CapturingGatewayWebApplicationFactory();
        factory.AccountHandler.ResponseStatusCode = HttpStatusCode.InternalServerError;
        using var client = factory.CreateClient();

        var resilience = new AccountServiceResilienceOptions();
        var attemptsPerEvent = 1 + resilience.MaxRetryAttempts;

        for (var attempt = 1; attempt <= resilience.CircuitBreakerFailures; attempt++)
        {
            var request = IntegrationTestHelpers.CreateEvent(
                $"evt-cb-{attempt}-{Guid.NewGuid():N}",
                "acct-circuit-breaker");

            var response = await client.PostAsJsonAsync("/events", request);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }

        Assert.Equal(resilience.CircuitBreakerFailures * attemptsPerEvent, factory.AccountHandler.RequestCount);

        var fastStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var afterOpenRequest = IntegrationTestHelpers.CreateEvent(
            $"evt-cb-open-{Guid.NewGuid():N}",
            "acct-circuit-breaker");
        var afterOpenResponse = await client.PostAsJsonAsync("/events", afterOpenRequest);
        fastStopwatch.Stop();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, afterOpenResponse.StatusCode);
        Assert.Equal(resilience.CircuitBreakerFailures * attemptsPerEvent, factory.AccountHandler.RequestCount);

        var problem = await afterOpenResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("Circuit breaker", problem.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.True(fastStopwatch.Elapsed < TimeSpan.FromSeconds(2), "Open circuit should fail fast.");
    }
}
