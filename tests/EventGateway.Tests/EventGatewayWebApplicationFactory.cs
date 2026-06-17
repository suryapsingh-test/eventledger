using EventGateway.Clients;
using EventLedger.Contracts.Accounts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EventGateway.Tests;

public class EventGatewayWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath;
    private readonly bool _ownsDatabase;

    public EventGatewayWebApplicationFactory()
    {
        _ownsDatabase = true;
        _dbPath = Path.Combine(Path.GetTempPath(), $"gateway-test-{Guid.NewGuid():N}.db");
    }

    protected EventGatewayWebApplicationFactory(string databasePath, bool ownsDatabase)
    {
        _dbPath = databasePath;
        _ownsDatabase = ownsDatabase;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAccountServiceClient>();
            services.AddSingleton<IAccountServiceClient>(CreateAccountServiceClient());
        });

        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbPath}");
        builder.UseSetting("AccountService:BaseUrl", "http://localhost:8081");
    }

    protected virtual IAccountServiceClient CreateAccountServiceClient() =>
        new StubAccountServiceClient();

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && _ownsDatabase && File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { /* best effort */ }
        }
    }
}

internal sealed class StubAccountServiceClient : IAccountServiceClient
{
    private readonly HashSet<string> _appliedEvents = new(StringComparer.Ordinal);

    public Task<AccountTransactionResult> ApplyTransactionAsync(
        string accountId,
        TransactionRequest request,
        string? traceParent,
        CancellationToken cancellationToken = default)
    {
        if (_appliedEvents.Contains(request.EventId))
            return Task.FromResult(new AccountTransactionResult(Success: true, IsReplay: true));

        _appliedEvents.Add(request.EventId);
        return Task.FromResult(new AccountTransactionResult(Success: true, IsReplay: false));
    }
}

public sealed class CountingAccountServiceClient : IAccountServiceClient
{
    private readonly HashSet<string> _appliedEvents = new(StringComparer.Ordinal);

    public int ApplyCallCount { get; private set; }

    public void ResetCallCount() => ApplyCallCount = 0;

    public Task<AccountTransactionResult> ApplyTransactionAsync(
        string accountId,
        TransactionRequest request,
        string? traceParent,
        CancellationToken cancellationToken = default)
    {
        ApplyCallCount++;

        if (_appliedEvents.Contains(request.EventId))
            return Task.FromResult(new AccountTransactionResult(Success: true, IsReplay: true));

        _appliedEvents.Add(request.EventId);
        return Task.FromResult(new AccountTransactionResult(Success: true, IsReplay: false));
    }
}

internal sealed class FailingAccountServiceClient : IAccountServiceClient
{
    public Task<AccountTransactionResult> ApplyTransactionAsync(
        string accountId,
        TransactionRequest request,
        string? traceParent,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new AccountTransactionResult(
            Success: false,
            IsReplay: false,
            FailureReason: "Account Service unavailable"));
}

public sealed class CountingAccountWebApplicationFactory : EventGatewayWebApplicationFactory
{
    public CountingAccountServiceClient AccountClient { get; } = new();

    protected override IAccountServiceClient CreateAccountServiceClient() => AccountClient;
}

public sealed class FailingAccountWebApplicationFactory : EventGatewayWebApplicationFactory
{
    public FailingAccountWebApplicationFactory()
    {
    }

    public FailingAccountWebApplicationFactory(string databasePath)
        : base(databasePath, ownsDatabase: false)
    {
    }

    protected override IAccountServiceClient CreateAccountServiceClient() =>
        new FailingAccountServiceClient();
}

public sealed class SharedDatabaseEventGatewayWebApplicationFactory : EventGatewayWebApplicationFactory
{
    public SharedDatabaseEventGatewayWebApplicationFactory(string databasePath)
        : base(databasePath, ownsDatabase: false)
    {
    }
}
