using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SharedKernel.Observability;

var builder = WebApplication.CreateBuilder(args);
builder.AddCampusSerilog("AnalyticsService");

builder.Services
    .AddHealthChecks()
    .AddCheck(
        "self",
        () => HealthCheckResult.Healthy(
            "AnalyticsService está disponible."));

var app = builder.Build();
app.UseCorrelationId();
app.UseCampusRequestLogging();

app.MapHealthChecks(
    "/api/analytics/health",
    new HealthCheckOptions
    {
        ResponseWriter = (context, report) =>
            HealthCheckResponseWriter.WriteAsync(
                context,
                report,
                "AnalyticsService")
    });

app.Run();
