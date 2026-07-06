using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;

namespace EventGateway.Resilience;

public static class AccountServiceResilienceExtensions
{
    public static IHttpClientBuilder AddAccountServiceResiliencePolicies(this IHttpClientBuilder builder)
    {
        builder.Services.TryAddSingleton<AccountServiceResiliencePolicies>();

        return builder
            .AddPolicyHandler((sp, _) => sp.GetRequiredService<AccountServiceResiliencePolicies>().Combined);
    }

    internal static IAsyncPolicy<HttpResponseMessage> CreateTimeoutPolicy(AccountServiceResilienceOptions options) =>
        Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(options.TimeoutSeconds));

    internal static IAsyncPolicy<HttpResponseMessage> CreateRetryPolicy(AccountServiceResilienceOptions options)
    {
        if (options.MaxRetryAttempts <= 0)
        {
            return Policy.NoOpAsync<HttpResponseMessage>();
        }

        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .Or<TimeoutRejectedException>()
            .WaitAndRetryAsync(
                options.MaxRetryAttempts,
                attempt => ComputeRetryDelay(attempt, options.RetryBaseDelayMs));
    }

    internal static IAsyncPolicy<HttpResponseMessage> CreateCircuitBreakerPolicy(AccountServiceResilienceOptions options) =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .Or<TimeoutRejectedException>()
            .CircuitBreakerAsync(
                options.CircuitBreakerFailures,
                TimeSpan.FromSeconds(options.CircuitBreakerBreakSeconds));

    internal static TimeSpan ComputeRetryDelay(int retryAttempt, int baseDelayMs)
    {
        var exponentialMs = baseDelayMs * Math.Pow(2, retryAttempt - 1);
        var jitterMs = Random.Shared.Next(0, (int)exponentialMs + 1);
        return TimeSpan.FromMilliseconds(exponentialMs + jitterMs);
    }
}

internal sealed class AccountServiceResiliencePolicies
{
    public AccountServiceResiliencePolicies(IOptions<AccountServiceResilienceOptions> options)
    {
        var resilience = options.Value;
        var timeout = AccountServiceResilienceExtensions.CreateTimeoutPolicy(resilience);
        var retry = AccountServiceResilienceExtensions.CreateRetryPolicy(resilience);
        var circuitBreaker = AccountServiceResilienceExtensions.CreateCircuitBreakerPolicy(resilience);

        // Outermost: circuit breaker → retry → timeout (per attempt) → HTTP
        Combined = Policy.WrapAsync(circuitBreaker, retry, timeout);
    }

    public IAsyncPolicy<HttpResponseMessage> Combined { get; }
}
