using System.Net;
using System.Net.Http.Json;
using EventGateway.Clients;
using EventLedger.Contracts.Accounts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace EventGateway.Tests;

/// <summary>Inbound bulkhead (concurrency) and per-client write rate limiting.</summary>
public sealed class InboundResilienceTests
{
    [Fact]
    public async Task PostEvent_ExceedsPerClientWriteRateLimit_Returns429()
    {
        using var factory = new InboundTestWebApplicationFactory(
            concurrencyLimit: 100,
            concurrencyQueueLimit: 50,
            perClientWritePermitLimit: 2,
            perClientWriteWindowSeconds: 60);

        using var client = factory.CreateClient();

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var response = await PostUniqueEventAsync(client, $"evt-rate-{attempt}");
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        var throttled = await PostUniqueEventAsync(client, "evt-rate-3");
        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);

        var problem = await throttled.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Too many requests", problem.Title);
    }

    [Fact]
    public async Task PostEvent_ExceedsInboundConcurrencyBulkhead_Returns429()
    {
        using var factory = new InboundTestWebApplicationFactory(
            concurrencyLimit: 1,
            concurrencyQueueLimit: 0,
            perClientWritePermitLimit: 100,
            perClientWriteWindowSeconds: 60,
            accountServiceDelay: TimeSpan.FromSeconds(2));

        using var client = factory.CreateClient();

        var slowRequest = PostUniqueEventAsync(client, $"evt-bulkhead-{Guid.NewGuid():N}");
        await Task.Delay(100);
        var competingRequest = PostUniqueEventAsync(client, $"evt-bulkhead-{Guid.NewGuid():N}");

        var results = await Task.WhenAll(slowRequest, competingRequest);

        Assert.Contains(HttpStatusCode.Created, results.Select(response => response.StatusCode));
        Assert.Contains(HttpStatusCode.TooManyRequests, results.Select(response => response.StatusCode));
    }

    [Fact]
    public async Task GetHealth_IsExemptFromInboundBulkhead()
    {
        using var factory = new InboundTestWebApplicationFactory(
            concurrencyLimit: 1,
            concurrencyQueueLimit: 0,
            perClientWritePermitLimit: 100,
            perClientWriteWindowSeconds: 60,
            accountServiceDelay: TimeSpan.FromSeconds(5));

        using var client = factory.CreateClient();

        _ = PostUniqueEventAsync(client, $"evt-health-block-{Guid.NewGuid():N}");
        await Task.Delay(100);

        var healthResponse = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);
    }

    private static Task<HttpResponseMessage> PostUniqueEventAsync(HttpClient client, string eventId) =>
        client.PostAsJsonAsync("/events", GatewayTestHelpers.CreateValidEventRequest(eventId, $"acct-{eventId}"));
}

internal sealed class InboundTestWebApplicationFactory : EventGatewayWebApplicationFactory
{
    private readonly int _concurrencyLimit;
    private readonly int _concurrencyQueueLimit;
    private readonly int _perClientWritePermitLimit;
    private readonly int _perClientWriteWindowSeconds;
    private readonly TimeSpan? _accountServiceDelay;

    public InboundTestWebApplicationFactory(
        int concurrencyLimit,
        int concurrencyQueueLimit,
        int perClientWritePermitLimit,
        int perClientWriteWindowSeconds,
        TimeSpan? accountServiceDelay = null)
    {
        _concurrencyLimit = concurrencyLimit;
        _concurrencyQueueLimit = concurrencyQueueLimit;
        _perClientWritePermitLimit = perClientWritePermitLimit;
        _perClientWriteWindowSeconds = perClientWriteWindowSeconds;
        _accountServiceDelay = accountServiceDelay;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.UseSetting("Gateway:Inbound:ConcurrencyLimit", _concurrencyLimit.ToString());
        builder.UseSetting("Gateway:Inbound:ConcurrencyQueueLimit", _concurrencyQueueLimit.ToString());
        builder.UseSetting("Gateway:Inbound:PerClientWritePermitLimit", _perClientWritePermitLimit.ToString());
        builder.UseSetting("Gateway:Inbound:PerClientWriteWindowSeconds", _perClientWriteWindowSeconds.ToString());
    }

    protected override IAccountServiceClient CreateAccountServiceClient() =>
        _accountServiceDelay is null
            ? base.CreateAccountServiceClient()
            : new DelayingAccountServiceClient(_accountServiceDelay.Value);
}

internal sealed class DelayingAccountServiceClient(TimeSpan delay) : IAccountServiceClient
{
    private readonly HashSet<string> _appliedEvents = new(StringComparer.Ordinal);

    public async Task<AccountTransactionResult> ApplyTransactionAsync(
        string accountId,
        TransactionRequest request,
        string? traceParent,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(delay, cancellationToken);

        if (_appliedEvents.Contains(request.EventId))
        {
            return new AccountTransactionResult(Success: true, IsReplay: true);
        }

        _appliedEvents.Add(request.EventId);
        return new AccountTransactionResult(Success: true, IsReplay: false);
    }
}
