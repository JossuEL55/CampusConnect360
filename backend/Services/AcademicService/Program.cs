using SharedKernel.Observability;

var builder = WebApplication.CreateBuilder(args);
builder.AddCampusSerilog("AcademicService");

var app = builder.Build();
app.UseCorrelationId();
app.UseCampusRequestLogging();

app.MapGet("/api/academic/health", (HttpContext context) =>
{
    var correlationId =
        context.Items[
            CorrelationConstants.LogPropertyName
        ]?.ToString();

    return Results.Ok(new
    {
        service = "AcademicService",
        status = "Healthy",
        correlationId,
        timestamp = DateTimeOffset.UtcNow
    });
});

app.MapGet(
    "/api/academic/secure-test",
    () => Results.Ok(new
    {
        service = "AcademicService",
        access = "Granted"
    }));

app.Run();
