using EventGateway.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace EventLedger.IntegrationTests;

/// <summary>
/// Gateway host wired to Account Service via TestServer handler (real HTTP pipeline).
/// </summary>
public sealed class E2EGatewayWebApplicationFactory : WebApplicationFactory<EventService>
{
    private readonly string _accountServiceBaseUrl;
    private readonly HttpMessageHandler _accountServiceHandler;
    private readonly string _dbPath;

    public E2EGatewayWebApplicationFactory(
        string accountServiceBaseUrl,
        HttpMessageHandler accountServiceHandler)
    {
        _accountServiceBaseUrl = accountServiceBaseUrl;
        _accountServiceHandler = accountServiceHandler;
        _dbPath = Path.Combine(Path.GetTempPath(), $"gateway-integ-{Guid.NewGuid():N}.db");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbPath}");
        builder.UseSetting("AccountService:BaseUrl", _accountServiceBaseUrl);

        builder.ConfigureTestServices(services =>
        {
            services.AddHttpClient("AccountService")
                .ConfigurePrimaryHttpMessageHandler(_ => _accountServiceHandler);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { /* best effort */ }
        }
    }
}
