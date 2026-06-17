using EventLedger.Contracts.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AccountService.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", async (HealthCheckService healthCheckService, CancellationToken cancellationToken) =>
        {
            var report = await healthCheckService.CheckHealthAsync(cancellationToken);
            var databaseStatus = report.Entries.TryGetValue("database", out var databaseEntry)
                ? databaseEntry.Status == HealthStatus.Healthy ? "Healthy" : "Unhealthy"
                : "Unhealthy";

            var isHealthy = report.Status == HealthStatus.Healthy;
            var response = new HealthResponse(
                isHealthy ? "Healthy" : "Unhealthy",
                "AccountService",
                new Dictionary<string, string> { ["database"] = databaseStatus },
                DateTimeOffset.UtcNow.ToString("O"));

            return isHealthy
                ? Results.Ok(response)
                : Results.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable);
        });

        return app;
    }
}
