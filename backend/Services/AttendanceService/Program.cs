using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SharedKernel.Observability;

var builder = WebApplication.CreateBuilder(args);
builder.AddCampusSerilog("AttendanceService");

builder.Services
    .AddHealthChecks()
    .AddCheck(
        "self",
        () => HealthCheckResult.Healthy(
            "AttendanceService está disponible."));

var app = builder.Build();
app.UseCorrelationId();
app.UseCampusRequestLogging();

app.MapHealthChecks(
    "/api/attendance/health",
    new HealthCheckOptions
    {
        ResponseWriter = (context, report) =>
            HealthCheckResponseWriter.WriteAsync(
                context,
                report,
                "AttendanceService")
    });

app.Run();
