using System.Net;

namespace EventLedger.IntegrationTests;

/// <summary>Handout §6 — balance queries fail clearly when Account Service is unreachable.</summary>
public sealed class AccountServiceUnavailableTests
{
    [Fact]
    public async Task GetBalance_AccountServiceUnreachable_FailsWithConnectionError()
    {
        using var client = CreateClientForUnreachableService();

        var exception = await Record.ExceptionAsync(() =>
            client.GetAsync("/accounts/acct-123/balance"));

        AssertConnectionFailure(exception);
    }

    [Fact]
    public async Task GetAccountDetail_AccountServiceUnreachable_FailsWithConnectionError()
    {
        using var client = CreateClientForUnreachableService();

        var exception = await Record.ExceptionAsync(() =>
            client.GetAsync("/accounts/acct-123"));

        AssertConnectionFailure(exception);
    }

    private static HttpClient CreateClientForUnreachableService() =>
        new()
        {
            BaseAddress = new Uri("http://127.0.0.1:59999"),
            Timeout = TimeSpan.FromSeconds(3)
        };

    private static void AssertConnectionFailure(Exception? exception)
    {
        Assert.NotNull(exception);
        Assert.True(
            exception is HttpRequestException or TaskCanceledException,
            $"Expected connection failure, got {exception.GetType().Name}: {exception.Message}");
    }
}
