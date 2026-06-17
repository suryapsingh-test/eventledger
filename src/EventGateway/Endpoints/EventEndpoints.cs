using System.Diagnostics;
using System.Text.Json;
using EventGateway.Metrics;
using EventGateway.Services;
using EventLedger.Contracts.Events;
using EventLedger.Contracts.Headers;
using Microsoft.AspNetCore.Mvc;

namespace EventGateway.Endpoints;

public static class EventEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static IEndpointRouteBuilder MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/events", SubmitEventAsync);
        app.MapGet("/events/{eventId}", GetEventByIdAsync);
        app.MapGet("/events", GetEventsByAccountAsync);
        return app;
    }

    private static async Task<IResult> SubmitEventAsync(
        HttpContext httpContext,
        EventRequest request,
        EventService eventService,
        EventMetrics eventMetrics,
        CancellationToken cancellationToken)
    {
        var payloadJson = JsonSerializer.Serialize(request, JsonOptions);

        var (response, statusCode, isReplay) = await eventService.SubmitEventAsync(
            request,
            payloadJson,
            cancellationToken);

        if (isReplay)
            httpContext.Response.Headers[EventLedgerHeaders.IdempotencyReplay] = "true";
        else
            eventMetrics.RecordProcessed();

        return Results.Json(response, statusCode: statusCode);
    }

    private static async Task<IResult> GetEventByIdAsync(
        string eventId,
        EventService eventService,
        CancellationToken cancellationToken)
    {
        var response = await eventService.GetEventByIdAsync(eventId, cancellationToken);
        if (response is null)
        {
            return Results.Problem(
                title: "Event not found",
                detail: $"No event with id '{eventId}' was found.",
                statusCode: StatusCodes.Status404NotFound,
                type: "https://eventledger/errors/not-found",
                extensions: new Dictionary<string, object?> { ["traceId"] = GetTraceId() });
        }

        return Results.Ok(response);
    }

    private static async Task<IResult> GetEventsByAccountAsync(
        [FromQuery(Name = "account")] string? account,
        EventService eventService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(account))
        {
            return Results.Problem(
                title: "Validation failed",
                detail: "account query parameter is required",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://eventledger/errors/validation",
                extensions: new Dictionary<string, object?> { ["traceId"] = GetTraceId() });
        }

        var events = await eventService.GetEventsByAccountAsync(account, cancellationToken);
        return Results.Ok(events);
    }

    private static string? GetTraceId() => Activity.Current?.TraceId.ToString();
}
