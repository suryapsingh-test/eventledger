using System.Net;
using System.Net.Http.Json;
using EventLedger.Contracts.Accounts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using AccountService.Data;

namespace AccountService.Tests;

public sealed class LegacySchemaMigrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly LegacyAccountServiceWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public LegacySchemaMigrationTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        CreateLegacySchema(_connection);

        _factory = new LegacyAccountServiceWebApplicationFactory(_connection);
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task LegacyDatabaseWithoutBalanceColumn_IsPatchedOnStartup()
    {
        var response = await _client.GetAsync("/accounts/acct-legacy-001/balance");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var balance = await response.Content.ReadFromJsonAsync<BalanceResponse>();
        Assert.NotNull(balance);
        Assert.Equal("acct-legacy-001", balance.AccountId);
        Assert.Equal(110.00m, balance.Balance);
    }

    [Fact]
    public async Task LegacyDatabaseWithoutBalanceColumn_AccountDetailWorks()
    {
        var response = await _client.GetAsync("/accounts/acct-legacy-001");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var account = await response.Content.ReadFromJsonAsync<AccountDetailResponse>();
        Assert.NotNull(account);
        Assert.Equal(110.00m, account.Balance);
        Assert.Equal(2, account.RecentTransactions.Count);
    }

    private static void CreateLegacySchema(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE Accounts (
                AccountId TEXT NOT NULL PRIMARY KEY,
                CreatedAt TEXT NOT NULL,
                Currency TEXT NOT NULL
            );

            CREATE TABLE Transactions (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                EventId TEXT NOT NULL,
                AccountId TEXT NOT NULL,
                Type TEXT NOT NULL,
                Amount TEXT NOT NULL,
                Currency TEXT NOT NULL,
                EventTimestamp TEXT NOT NULL,
                AppliedAt TEXT NOT NULL
            );

            CREATE UNIQUE INDEX IX_Transactions_EventId ON Transactions (EventId);

            INSERT INTO Accounts (AccountId, CreatedAt, Currency)
            VALUES ('acct-legacy-001', '2026-06-26T16:00:00Z', 'USD');

            INSERT INTO Transactions (EventId, AccountId, Type, Amount, Currency, EventTimestamp, AppliedAt)
            VALUES
                ('evt-legacy-credit', 'acct-legacy-001', 'CREDIT', '150.00', 'USD', '2026-06-26T16:00:00Z', '2026-06-26T16:00:01Z'),
                ('evt-legacy-debit', 'acct-legacy-001', 'DEBIT', '40.00', 'USD', '2026-06-26T16:05:00Z', '2026-06-26T16:05:01Z');
            """;
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        _connection.Dispose();
    }
}

internal sealed class LegacyAccountServiceWebApplicationFactory(SqliteConnection connection)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll(typeof(DbContextOptions<AccountDbContext>));
            services.RemoveAll(typeof(AccountDbContext));

            services.AddDbContext<AccountDbContext>(options =>
                options.UseSqlite(connection));
        });
    }
}
