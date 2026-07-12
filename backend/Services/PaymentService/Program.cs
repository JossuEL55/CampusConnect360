using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SharedKernel.Observability;

var builder = WebApplication.CreateBuilder(args);
builder.AddCampusSerilog("PaymentService");

builder.Services
    .AddHealthChecks()
    .AddCheck(
        "self",
        () => HealthCheckResult.Healthy(
            "PaymentService está disponible."));

var app = builder.Build();
app.UseCorrelationId();
app.UseCampusRequestLogging();

app.MapHealthChecks(
    "/api/payments/health",
    new HealthCheckOptions
    {
        ResponseWriter = (context, report) =>
            HealthCheckResponseWriter.WriteAsync(
                context,
                report,
                "PaymentService")
    });

app.Run();
