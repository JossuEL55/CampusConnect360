using SharedKernel.Observability;

var builder = WebApplication.CreateBuilder(args);
builder.AddCampusSerilog("AttendanceService");
var app = builder.Build();
app.UseCorrelationId();
app.UseCampusRequestLogging();

app.MapGet("/api/attendance/health", (HttpContext context) =>
{
    var correlationId =
        context.Items[
            CorrelationConstants.LogPropertyName
        ]?.ToString();

    return Results.Ok(new
    {
        service = "AttendanceService",
        status = "Healthy",
        correlationId,
        timestamp = DateTimeOffset.UtcNow
    });
});

app.Run();
