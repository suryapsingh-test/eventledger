using AccountService.Data;
using AccountService.Endpoints;
using AccountService.Extensions;
using AccountService.Logging;
using AccountService.Middleware;
using AccountService.Services;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, _, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.With(new TraceIdEnricher())
        .Enrich.WithProperty("ServiceName", "AccountService")
        .WriteTo.Console(new CompactJsonFormatter());
});

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("AccountService"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddConsoleExporter());

builder.Services.AddDbContext<AccountDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AccountDbContext>("database");

builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<IAccountQueryService, AccountQueryService>();

builder.Services.AddProblemDetails();
builder.Services.AddAccountServiceSwagger();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
    await AccountDbInitializer.InitializeAsync(dbContext);
}

app.UseGlobalExceptionHandling();
app.UseSerilogRequestLogging();
app.UseAccountServiceSwagger();

app.MapAccountEndpoints();
app.MapHealthEndpoints();

app.Run();

public partial class Program;
