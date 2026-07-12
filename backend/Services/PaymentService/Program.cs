using SharedKernel.Observability;

var builder = WebApplication.CreateBuilder(args);
builder.AddCampusSerilog("PaymentService");
var app = builder.Build();
app.UseCorrelationId();
app.UseCampusRequestLogging();

app.MapGet("/api/payments/health", (HttpContext context) =>
{
    var correlationId =
        context.Items[
            CorrelationConstants.LogPropertyName
        ]?.ToString();

    return Results.Ok(new
    {
        service = "PaymentService",
        status = "Healthy",
        correlationId,
        timestamp = DateTimeOffset.UtcNow
    });
});

app.Run();
