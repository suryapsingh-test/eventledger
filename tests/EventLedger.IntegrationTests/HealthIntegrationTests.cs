using System.Net;
using System.Net.Http.Json;
using EventLedger.Contracts.Health;

namespace EventLedger.IntegrationTests;

/// <summary>AC-12 — Both services report healthy when databases are up.</summary>
public sealed class HealthIntegrationTests(EventLedgerIntegrationFixture fixture)
    : IClassFixture<EventLedgerIntegrationFixture>
{
    private readonly HttpClient _gateway = fixture.GatewayClient;
    private readonly HttpClient _account = fixture.AccountClient;

    [Fact]
    public async Task GatewayHealth_ReturnsHealthy()
    {
        var response = await _gateway.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var health = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.NotNull(health);
        Assert.Equal("Healthy", health.Status);
        Assert.Equal("EventGateway", health.Service);
        Assert.Equal("Healthy", health.Checks["database"]);
    }

    [Fact]
    public async Task AccountServiceHealth_ReturnsHealthy()
    {
        var response = await _account.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var health = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.NotNull(health);
        Assert.Equal("Healthy", health.Status);
        Assert.Equal("AccountService", health.Service);
        Assert.Equal("Healthy", health.Checks["database"]);
    }
}
