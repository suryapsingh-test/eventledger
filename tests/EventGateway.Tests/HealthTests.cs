using System.Net;
using System.Net.Http.Json;
using EventLedger.Contracts.Health;

namespace EventGateway.Tests;

/// <summary>AC-12 — Gateway GET /health.</summary>
public sealed class HealthTests(EventGatewayWebApplicationFactory factory)
    : IClassFixture<EventGatewayWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Health_ReturnsHealthyWithDatabaseDiagnostics()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var health = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.NotNull(health);
        Assert.Equal("Healthy", health.Status);
        Assert.Equal("EventGateway", health.Service);
        Assert.Equal("Healthy", health.Checks["database"]);
        Assert.False(string.IsNullOrWhiteSpace(health.Timestamp));
    }
}
