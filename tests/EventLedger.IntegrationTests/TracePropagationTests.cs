using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using EventLedger.IntegrationTests.Infrastructure;

namespace EventLedger.IntegrationTests;

/// <summary>AC-16, AC-17 — trace propagation Gateway → Account via HTTP header.</summary>
public sealed class TracePropagationTests : IDisposable
{
    private static readonly Regex W3CTraceParentPattern = new(
        @"^00-[0-9a-f]{32}-[0-9a-f]{16}-[0-9a-f]{2}$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly CapturingGatewayWebApplicationFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task PostEvent_ForwardsTraceParentHeaderToAccountService()
    {
        _factory.AccountHandler.ResponseStatusCode = HttpStatusCode.Created;
        using var client = _factory.CreateClient();

        var eventId = $"evt-trace-{Guid.NewGuid():N}";
        var request = IntegrationTestHelpers.CreateEvent(eventId, "acct-trace");

        var response = await client.PostAsJsonAsync("/events", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1, _factory.AccountHandler.RequestCount);

        var traceParent = _factory.AccountHandler.LastTraceParent;
        Assert.False(string.IsNullOrWhiteSpace(traceParent));
        Assert.Matches(W3CTraceParentPattern, traceParent);
    }

    [Fact]
    public async Task PostEvent_DownstreamTraceParentMatchesGatewayTraceIdOnFailure()
    {
        _factory.AccountHandler.ResponseStatusCode = HttpStatusCode.InternalServerError;
        using var client = _factory.CreateClient();

        var request = IntegrationTestHelpers.CreateEvent(
            $"evt-trace-fail-{Guid.NewGuid():N}",
            "acct-trace-fail");

        var response = await client.PostAsJsonAsync("/events", request);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        using var document = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(document);

        var traceId = document.RootElement.GetProperty("traceId").GetString();
        var downstreamTraceId = IntegrationTestHelpers.ExtractTraceIdFromTraceParent(
            _factory.AccountHandler.LastTraceParent);

        Assert.False(string.IsNullOrWhiteSpace(traceId));
        Assert.Equal(traceId, downstreamTraceId);
    }

    [Fact]
    public async Task PostEvent_SuccessfulApply_UsesConsistentTraceAcrossDownstreamCall()
    {
        _factory.AccountHandler.ResponseStatusCode = HttpStatusCode.Created;
        using var client = _factory.CreateClient();

        var request = IntegrationTestHelpers.CreateEvent(
            $"evt-trace-ok-{Guid.NewGuid():N}",
            "acct-trace-ok");

        var response = await client.PostAsJsonAsync("/events", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var captured = _factory.AccountHandler.Requests.Single();
        Assert.True(captured.Headers.Contains("traceparent"));

        var traceParent = _factory.AccountHandler.LastTraceParent;
        Assert.NotNull(traceParent);
        Assert.Matches(W3CTraceParentPattern, traceParent);

        var downstreamRequest = captured.RequestUri?.AbsolutePath ?? string.Empty;
        Assert.Contains("/accounts/", downstreamRequest, StringComparison.Ordinal);
        Assert.Contains("/transactions", downstreamRequest, StringComparison.Ordinal);
    }
}
