namespace EventLedger.IntegrationTests;

/// <summary>
/// Starts Account Service and Gateway with real HTTP between them.
/// </summary>
public sealed class EventLedgerIntegrationFixture : IDisposable
{
    public AccountIntegrationWebApplicationFactory AccountFactory { get; }
    public E2EGatewayWebApplicationFactory GatewayFactory { get; }
    public HttpClient AccountClient { get; }
    public HttpClient GatewayClient { get; }

    public EventLedgerIntegrationFixture()
    {
        AccountFactory = new AccountIntegrationWebApplicationFactory();
        AccountClient = AccountFactory.CreateClient();

        var accountBaseUrl = AccountFactory.Server.BaseAddress!.ToString().TrimEnd('/');
        GatewayFactory = new E2EGatewayWebApplicationFactory(
            accountBaseUrl,
            AccountFactory.Server.CreateHandler());
        GatewayClient = GatewayFactory.CreateClient();
    }

    public void Dispose()
    {
        GatewayClient.Dispose();
        GatewayFactory.Dispose();
        AccountClient.Dispose();
        AccountFactory.Dispose();
    }
}
