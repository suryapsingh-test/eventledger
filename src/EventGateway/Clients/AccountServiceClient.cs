using System.Net.Http.Json;
using EventLedger.Contracts.Accounts;
using EventLedger.Contracts.Headers;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace EventGateway.Clients;

public sealed class AccountServiceOptions
{
    public const string SectionName = "AccountService";
    public string BaseUrl { get; set; } = "http://localhost:8081";
}

public sealed class AccountServiceClient(
    IHttpClientFactory httpClientFactory,
    IOptions<AccountServiceOptions> options,
    ILogger<AccountServiceClient> logger) : IAccountServiceClient
{
    public async Task<AccountTransactionResult> ApplyTransactionAsync(
        string accountId,
        TransactionRequest request,
        string? traceParent,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("AccountService");
        var baseUrl = options.Value.BaseUrl.TrimEnd('/');
        var requestUri = $"{baseUrl}/accounts/{Uri.EscapeDataString(accountId)}/transactions";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(request)
        };

        if (!string.IsNullOrEmpty(traceParent))
            httpRequest.Headers.TryAddWithoutValidation("traceparent", traceParent);

        try
        {
            using var response = await client.SendAsync(httpRequest, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var isReplay = response.Headers.TryGetValues(
                    EventLedgerHeaders.IdempotencyReplay,
                    out var values) && values.Any(v => string.Equals(v, "true", StringComparison.OrdinalIgnoreCase));

                return new AccountTransactionResult(Success: true, IsReplay: isReplay);
            }

            logger.LogWarning(
                "Account Service returned {StatusCode} for event {EventId}",
                (int)response.StatusCode,
                request.EventId);

            return new AccountTransactionResult(
                Success: false,
                IsReplay: false,
                FailureReason: $"Account Service returned {(int)response.StatusCode}");
        }
        catch (BrokenCircuitException ex)
        {
            logger.LogError(ex, "Circuit breaker open for event {EventId}", request.EventId);
            return new AccountTransactionResult(
                Success: false,
                IsReplay: false,
                FailureReason: "Unable to apply transaction. Circuit breaker is open.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutRejectedException)
        {
            logger.LogError(ex, "Account Service call failed for event {EventId}", request.EventId);
            return new AccountTransactionResult(
                Success: false,
                IsReplay: false,
                FailureReason: "Unable to apply transaction. Account Service is unavailable.");
        }
    }
}
