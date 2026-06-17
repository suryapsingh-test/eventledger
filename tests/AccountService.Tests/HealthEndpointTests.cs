using System.Net;
using System.Net.Http.Json;
using EventLedger.Contracts.Health;

namespace AccountService.Tests;

public sealed class HealthEndpointTests(AccountServiceWebApplicationFactory factory) : IClassFixture<AccountServiceWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetHealth_ReturnsHealthyWithDatabaseCheck()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.NotNull(body);
        Assert.Equal("Healthy", body.Status);
        Assert.Equal("AccountService", body.Service);
        Assert.Equal("Healthy", body.Checks["database"]);
    }
}
