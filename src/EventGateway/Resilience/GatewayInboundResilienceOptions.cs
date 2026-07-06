namespace EventGateway.Resilience;

public sealed class GatewayInboundResilienceOptions
{
    public const string SectionName = "Gateway:Inbound";

    /// <summary>Max concurrent requests processed by the Gateway (bulkhead).</summary>
    public int ConcurrencyLimit { get; set; } = 100;

    /// <summary>Requests queued when concurrency limit is reached; excess returns 429.</summary>
    public int ConcurrencyQueueLimit { get; set; } = 50;

    /// <summary>Max POST /events submissions per client per window.</summary>
    public int PerClientWritePermitLimit { get; set; } = 300;

    public int PerClientWriteWindowSeconds { get; set; } = 60;
}
