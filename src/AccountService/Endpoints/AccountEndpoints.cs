using System.Diagnostics;
using EventLedger.Contracts.Accounts;
using EventLedger.Contracts.Headers;
using AccountService.Services;
using Microsoft.AspNetCore.Mvc;

namespace AccountService.Endpoints;

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/accounts");

        group.MapPost("/{accountId}/transactions", ApplyTransactionAsync);
        group.MapGet("/{accountId}/balance", GetBalanceAsync);
        group.MapGet("/{accountId}", GetAccountDetailAsync);

        return app;
    }

    private static async Task<IResult> ApplyTransactionAsync(
        string accountId,
        TransactionRequest request,
        ITransactionService transactionService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(accountId))
        {
            return Results.Problem(
                title: "Validation failed",
                detail: "accountId is required.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://eventledger/errors/validation",
                extensions: new Dictionary<string, object?> { ["traceId"] = GetTraceId() });
        }

        if (accountId.Length > 64)
        {
            return Results.Problem(
                title: "Validation failed",
                detail: "accountId must be at most 64 characters.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://eventledger/errors/validation",
                extensions: new Dictionary<string, object?> { ["traceId"] = GetTraceId() });
        }

        try
        {
            var (response, isReplay) = await transactionService.ApplyTransactionAsync(
                accountId,
                request,
                cancellationToken);

            if (isReplay)
            {
                return Results.Ok(response)
                    .WithHeader(EventLedgerHeaders.IdempotencyReplay, "true");
            }

            return Results.Created($"/accounts/{accountId}/transactions/{response.EventId}", response);
        }
        catch (ValidationException validationException)
        {
            return Results.Problem(
                title: "Validation failed",
                detail: string.Join(" ", validationException.Errors),
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://eventledger/errors/validation",
                extensions: new Dictionary<string, object?> { ["traceId"] = GetTraceId() });
        }
    }

    private static async Task<IResult> GetBalanceAsync(
        string accountId,
        IAccountQueryService accountQueryService,
        CancellationToken cancellationToken)
    {
        var balance = await accountQueryService.GetBalanceAsync(accountId, cancellationToken);
        if (balance is null)
        {
            return Results.Problem(
                title: "Account not found",
                detail: $"Account '{accountId}' was not found.",
                statusCode: StatusCodes.Status404NotFound,
                type: "https://eventledger/errors/not-found",
                extensions: new Dictionary<string, object?> { ["traceId"] = GetTraceId() });
        }

        return Results.Ok(balance);
    }

    private static async Task<IResult> GetAccountDetailAsync(
        string accountId,
        IAccountQueryService accountQueryService,
        CancellationToken cancellationToken)
    {
        var account = await accountQueryService.GetAccountDetailAsync(accountId, cancellationToken);
        if (account is null)
        {
            return Results.Problem(
                title: "Account not found",
                detail: $"Account '{accountId}' was not found.",
                statusCode: StatusCodes.Status404NotFound,
                type: "https://eventledger/errors/not-found",
                extensions: new Dictionary<string, object?> { ["traceId"] = GetTraceId() });
        }

        return Results.Ok(account);
    }

    private static string? GetTraceId() => Activity.Current?.TraceId.ToString();
}

internal static class ResultHeaderExtensions
{
    public static IResult WithHeader(this IResult result, string name, string value)
    {
        return new HeaderResult(result, name, value);
    }

    private sealed class HeaderResult(IResult inner, string name, string value) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.Headers[name] = value;
            await inner.ExecuteAsync(httpContext);
        }
    }
}
