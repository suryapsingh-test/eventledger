using Microsoft.OpenApi;

namespace AccountService.Extensions;

public static class SwaggerExtensions
{
    public static IServiceCollection AddAccountServiceSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Event Ledger — Account Service",
                Version = "v1",
                Description = "Internal API for account balances and transaction application."
            });
        });

        return services;
    }

    public static WebApplication UseAccountServiceSwagger(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
            return app;

        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Account Service v1");
            options.RoutePrefix = "swagger";
        });

        return app;
    }
}
