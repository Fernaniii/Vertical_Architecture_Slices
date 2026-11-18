using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace MyApp.ServiceDefaults;

/// <summary>
/// Shared defaults for every service in the solution. Kept to health checks for now;
/// OpenTelemetry, service discovery and resilience need extra packages and are added only if approved.
/// </summary>
public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Health endpoints expose internals, so they are only mapped in Development.
        if (app.Environment.IsDevelopment())
        {
            app.MapHealthChecks(HealthEndpointPath).AllowAnonymous();
            app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("live")
            }).AllowAnonymous();
        }

        return app;
    }
}
