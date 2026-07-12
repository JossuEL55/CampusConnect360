using SharedKernel.Observability;

var builder = WebApplication.CreateBuilder(args);
builder.AddCampusSerilog("AnalyticsService");
var app = builder.Build();
app.UseCorrelationId();
app.UseCampusRequestLogging();

app.MapGet("/api/analytics/health", (HttpContext context) =>
{
    var correlationId =
        context.Items[
            CorrelationConstants.LogPropertyName
        ]?.ToString();

    return Results.Ok(new
    {
        service = "AnalyticsService",
        status = "Healthy",
        correlationId,
        timestamp = DateTimeOffset.UtcNow
    });
});

app.Run();
