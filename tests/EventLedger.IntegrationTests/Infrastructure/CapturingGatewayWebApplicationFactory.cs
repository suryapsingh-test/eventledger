using EventGateway.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace EventLedger.IntegrationTests.Infrastructure;

/// <summary>
/// Gateway test host using the real AccountServiceClient and Polly policies
/// with a capturing HTTP handler for resiliency and trace assertions.
/// </summary>
public sealed class CapturingGatewayWebApplicationFactory : WebApplicationFactory<EventService>
{
    private readonly string _dbPath;
    private readonly bool _useCapturingHandler;

    public CapturingAccountServiceHandler AccountHandler { get; } = new();

    public CapturingGatewayWebApplicationFactory(bool useCapturingHandler = true)
    {
        _useCapturingHandler = useCapturingHandler;
        _dbPath = Path.Combine(Path.GetTempPath(), $"integration-gateway-{Guid.NewGuid():N}.db");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbPath}");
        builder.UseSetting(
            "AccountService:BaseUrl",
            _useCapturingHandler ? "http://account-service.test" : "http://127.0.0.1:1");

        if (_useCapturingHandler)
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(AccountHandler);
                services.AddHttpClient("AccountService")
                    .ConfigurePrimaryHttpMessageHandler(_ => AccountHandler);
            });
        }
    }

    protected override void Dispose(bool disposing)
    {
        AccountHandler.Dispose();

        if (disposing && File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { /* best effort */ }
        }

        base.Dispose(disposing);
    }
}
