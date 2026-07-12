using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SharedKernel.Observability;

var builder = WebApplication.CreateBuilder(args);
builder.AddCampusSerilog("AcademicService");

builder.Services
    .AddHealthChecks()
    .AddCheck(
        "self",
        () => HealthCheckResult.Healthy(
            "AcademicService está disponible."));

var app = builder.Build();
app.UseCorrelationId();
app.UseCampusRequestLogging();

app.MapHealthChecks(
    "/api/academic/health",
    new HealthCheckOptions
    {
        ResponseWriter = (context, report) =>
            HealthCheckResponseWriter.WriteAsync(
                context,
                report,
                "AcademicService")
    });

app.MapGet(
    "/api/academic/secure-test",
    () => Results.Ok(new
    {
        service = "AcademicService",
        access = "Granted"
    }));

app.Run();
