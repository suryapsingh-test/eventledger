using System.Diagnostics;
using EventGateway.Metrics;
using EventGateway.Services;
using Microsoft.AspNetCore.Mvc;

namespace EventGateway.Middleware;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (EventValidationException ex)
        {
            context.RequestServices.GetService<EventMetrics>()?.RecordFailed();
            await WriteProblemAsync(
                context,
                StatusCodes.Status400BadRequest,
                "Validation failed",
                ex.Message,
                "https://eventledger/errors/validation");
        }
        catch (AccountServiceUnavailableException ex)
        {
            context.RequestServices.GetService<EventMetrics>()?.RecordFailed();
            await WriteProblemAsync(
                context,
                StatusCodes.Status503ServiceUnavailable,
                "Account Service unavailable",
                ex.Detail,
                "https://eventledger/errors/service-unavailable");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);

            if (context.Response.HasStarted)
                throw;

            await WriteProblemAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred",
                "The server encountered an unexpected error.",
                "https://eventledger/errors/internal");
        }
    }

    private static async Task WriteProblemAsync(
        HttpContext context,
        int statusCode,
        string title,
        string detail,
        string type)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Type = type,
            Title = title,
            Status = statusCode,
            Detail = detail,
            Extensions = { ["traceId"] = Activity.Current?.TraceId.ToString() }
        };

        await context.Response.WriteAsJsonAsync(problem);
    }
}

public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app) =>
        app.UseMiddleware<ExceptionHandlingMiddleware>();
}
