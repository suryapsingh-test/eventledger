using EventGateway.Clients;
using EventGateway.Data;
using EventGateway.Endpoints;
using EventGateway.Extensions;
using EventGateway.Logging;
using EventGateway.Middleware;
using EventGateway.Metrics;
using EventGateway.Resilience;
using EventGateway.Services;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.With(new TraceIdEnricher())
    .Enrich.WithProperty("ServiceName", "EventGateway")
    .WriteTo.Console(new RenderedCompactJsonFormatter()));

builder.Services.Configure<AccountServiceOptions>(
    builder.Configuration.GetSection(AccountServiceOptions.SectionName));
builder.Services.Configure<AccountServiceResilienceOptions>(
    builder.Configuration.GetSection(AccountServiceResilienceOptions.SectionName));

builder.Services.AddDbContext<GatewayDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddSingleton<EventMetrics>();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("EventGateway"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddConsoleExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddMeter(EventMetrics.MeterName)
        .AddConsoleExporter());

builder.Services.AddHttpClient("AccountService")
    .AddAccountServiceResiliencePolicies();

builder.Services.AddScoped<IAccountServiceClient, AccountServiceClient>();
builder.Services.AddScoped<EventService>();

builder.Services.AddProblemDetails();
builder.Services.AddGatewayInboundResilience(builder.Configuration);
builder.Services.AddEventGatewaySwagger();

builder.WebHost.ConfigureKestrel(options =>
    options.Limits.MaxRequestBodySize = 64 * 1024);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GatewayDbContext>();
    db.Database.EnsureCreated();
}

app.UseGlobalExceptionHandling();
app.UseRateLimiter();
app.UseSerilogRequestLogging();
app.UseEventGatewaySwagger();

app.MapEventEndpoints();
app.MapHealthEndpoints();

app.Run();

public partial class Program;
