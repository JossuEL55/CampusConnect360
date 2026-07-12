using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SharedKernel.Observability;

var builder = WebApplication.CreateBuilder(args);
builder.AddCampusSerilog("NotificationService");

builder.Services
    .AddHealthChecks()
    .AddCheck(
        "self",
        () => HealthCheckResult.Healthy(
            "NotificationService está disponible."));

var app = builder.Build();
app.UseCorrelationId();
app.UseCampusRequestLogging();

app.MapHealthChecks(
    "/api/notifications/health",
    new HealthCheckOptions
    {
        ResponseWriter = (context, report) =>
            HealthCheckResponseWriter.WriteAsync(
                context,
                report,
                "NotificationService")
    });

app.Run();
