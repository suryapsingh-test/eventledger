using EventGateway.Data;
using EventLedger.Contracts.Health;

namespace EventGateway.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", GetHealthAsync)
            .WithName("GetHealth")
            .WithTags("Health")
            .WithSummary("Service health check")
            .Produces<HealthResponse>()
            .Produces<HealthResponse>(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    private static async Task<IResult> GetHealthAsync(GatewayDbContext db, CancellationToken cancellationToken)
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("O");
        string dbStatus;

        try
        {
            var canConnect = await db.Database.CanConnectAsync(cancellationToken);
            dbStatus = canConnect ? "Healthy" : "Unhealthy";
        }
        catch
        {
            dbStatus = "Unhealthy";
        }

        var isHealthy = dbStatus == "Healthy";
        var response = new HealthResponse(
            isHealthy ? "Healthy" : "Unhealthy",
            "EventGateway",
            new Dictionary<string, string> { ["database"] = dbStatus },
            timestamp);

        return Results.Json(
            response,
            statusCode: isHealthy ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
    }
}
