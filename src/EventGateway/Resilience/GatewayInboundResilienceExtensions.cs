using System.Globalization;
using System.Threading.RateLimiting;
using EventGateway.Metrics;
using Microsoft.AspNetCore.RateLimiting;

namespace EventGateway.Resilience;

public static class GatewayInboundResilienceExtensions
{
    public static IServiceCollection AddGatewayInboundResilience(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<GatewayInboundResilienceOptions>(
            configuration.GetSection(GatewayInboundResilienceOptions.SectionName));

        var inbound = configuration
            .GetSection(GatewayInboundResilienceOptions.SectionName)
            .Get<GatewayInboundResilienceOptions>() ?? new GatewayInboundResilienceOptions();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, cancellationToken) =>
            {
                var metrics = context.HttpContext.RequestServices.GetService<EventMetrics>();
                metrics?.RecordThrottled();

                var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfterValue)
                    ? (int)Math.Ceiling(retryAfterValue.TotalSeconds)
                    : (int?)null;

                if (retryAfter is > 0)
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        retryAfter.Value.ToString(CultureInfo.InvariantCulture);
                }

                await Results.Problem(
                        title: "Too many requests",
                        detail: "Inbound rate or concurrency limit exceeded. Retry later.",
                        statusCode: StatusCodes.Status429TooManyRequests,
                        type: "https://eventledger/errors/rate-limited",
                        extensions: new Dictionary<string, object?>
                        {
                            ["traceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString()
                        })
                    .ExecuteAsync(context.HttpContext);
            };

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                if (httpContext.Request.Path.StartsWithSegments("/health"))
                {
                    return RateLimitPartition.GetNoLimiter("health");
                }

                return RateLimitPartition.GetConcurrencyLimiter(
                    "gateway-inbound-bulkhead",
                    _ => new ConcurrencyLimiterOptions
                    {
                        PermitLimit = inbound.ConcurrencyLimit,
                        QueueLimit = inbound.ConcurrencyQueueLimit,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    });
            });

            options.AddPolicy(GatewayInboundPolicies.PerClientWrites, httpContext =>
            {
                var clientKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous";

                return RateLimitPartition.GetFixedWindowLimiter(
                    clientKey,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = inbound.PerClientWritePermitLimit,
                        Window = TimeSpan.FromSeconds(inbound.PerClientWriteWindowSeconds),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });
        });

        return services;
    }
}
