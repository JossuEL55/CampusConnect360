using SharedKernel.Observability;

var builder = WebApplication.CreateBuilder(args);
builder.AddCampusSerilog("NotificationService");
var app = builder.Build();
app.UseCorrelationId();
app.UseCampusRequestLogging();

app.MapGet("/api/notifications/health", (HttpContext context) =>
{
    var correlationId =
        context.Items[
            CorrelationConstants.LogPropertyName
        ]?.ToString();

    return Results.Ok(new
    {
        service = "NotificationService",
        status = "Healthy",
        correlationId,
        timestamp = DateTimeOffset.UtcNow
    });
});

app.Run();
