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
        app.MapPost("/events", SubmitEventAsync)
            .WithName("SubmitEvent")
            .WithTags("Events")
            .WithSummary("Submit a transaction event")
            .WithDescription("Creates a new event or returns the existing one when the same eventId is submitted again (Idempotency-Replay: true).")
            .Accepts<EventRequest>("application/json")
            .Produces<EventResponse>(StatusCodes.Status201Created)
            .Produces<EventResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        app.MapGet("/events/{eventId}", GetEventByIdAsync)
            .WithName("GetEventById")
            .WithTags("Events")
            .WithSummary("Get an event by ID")
            .Produces<EventResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/events", GetEventsByAccountAsync)
            .WithName("GetEventsByAccount")
            .WithTags("Events")
            .WithSummary("List events for an account")
            .Produces<IReadOnlyList<EventResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

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
