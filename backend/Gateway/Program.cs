using SharedKernel.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.AddCampusSerilog("Gateway");

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(
        builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseCorrelationId();
app.UseCampusRequestLogging();

app.MapGet("/health", (HttpContext context) =>
{
    var correlationId =
        context.Items[
            CorrelationConstants.LogPropertyName
        ]?.ToString();

    return Results.Ok(new
    {
        service = "Gateway",
        status = "Healthy",
        correlationId,
        timestamp = DateTimeOffset.UtcNow
    });
});

app.MapReverseProxy();

app.Run();