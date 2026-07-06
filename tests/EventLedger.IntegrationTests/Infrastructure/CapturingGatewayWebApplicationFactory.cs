using EventGateway.Resilience;
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
    private readonly Dictionary<string, string?> _resilienceSettings = new(StringComparer.OrdinalIgnoreCase);

    public CapturingAccountServiceHandler AccountHandler { get; } = new();

    public CapturingGatewayWebApplicationFactory(bool useCapturingHandler = true)
    {
        _useCapturingHandler = useCapturingHandler;
        _dbPath = Path.Combine(Path.GetTempPath(), $"integration-gateway-{Guid.NewGuid():N}.db");
    }

    public void WithResilienceOptions(AccountServiceResilienceOptions options)
    {
        _resilienceSettings["AccountService:Resilience:MaxRetryAttempts"] = options.MaxRetryAttempts.ToString();
        _resilienceSettings["AccountService:Resilience:RetryBaseDelayMs"] = options.RetryBaseDelayMs.ToString();
        _resilienceSettings["AccountService:Resilience:TimeoutSeconds"] = options.TimeoutSeconds.ToString();
        _resilienceSettings["AccountService:Resilience:CircuitBreakerFailures"] = options.CircuitBreakerFailures.ToString();
        _resilienceSettings["AccountService:Resilience:CircuitBreakerBreakSeconds"] = options.CircuitBreakerBreakSeconds.ToString();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbPath}");
        builder.UseSetting(
            "AccountService:BaseUrl",
            _useCapturingHandler ? "http://account-service.test" : "http://127.0.0.1:1");

        foreach (var (key, value) in _resilienceSettings)
        {
            builder.UseSetting(key, value);
        }

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
