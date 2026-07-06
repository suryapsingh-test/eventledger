namespace EventGateway.Resilience;

public sealed class AccountServiceResilienceOptions
{
    public const string SectionName = "AccountService:Resilience";

    public int MaxRetryAttempts { get; set; } = 3;

    public int RetryBaseDelayMs { get; set; } = 200;

    public int TimeoutSeconds { get; set; } = 5;

    public int CircuitBreakerFailures { get; set; } = 5;

    public int CircuitBreakerBreakSeconds { get; set; } = 30;
}
