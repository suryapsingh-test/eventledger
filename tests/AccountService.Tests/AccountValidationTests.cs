using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;

namespace AccountService.Tests;

/// <summary>SEC-004 — accountId length validation.</summary>
public sealed class AccountValidationTests(AccountServiceWebApplicationFactory factory) : IClassFixture<AccountServiceWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task PostTransaction_WithAccountIdTooLong_ReturnsBadRequest()
    {
        var accountId = new string('a', 65);
        var request = AccountServiceTestHelper.Credit("evt-sec004-long", 10.00m, "2026-05-15T10:00:00Z");

        var response = await AccountServiceTestHelper.PostTransactionAsync(_client, accountId, request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("64", problem.Detail, StringComparison.OrdinalIgnoreCase);
    }
}
