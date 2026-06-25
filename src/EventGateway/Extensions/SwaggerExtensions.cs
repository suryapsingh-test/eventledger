using Microsoft.OpenApi;

namespace EventGateway.Extensions;

public static class SwaggerExtensions
{
    public static IServiceCollection AddEventGatewaySwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Event Ledger — Event Gateway",
                Version = "v1",
                Description = "Public API for submitting and querying transaction events."
            });
        });

        return services;
    }

    public static WebApplication UseEventGatewaySwagger(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
            return app;

        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Event Gateway v1");
            options.RoutePrefix = "swagger";
        });

        return app;
    }
}
