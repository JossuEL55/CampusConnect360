using AnalyticsService.Data;
using AnalyticsService.Endpoints;
using AnalyticsService.Messaging;
using AnalyticsService.Monitoring;
using AnalyticsService.Projections;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using SharedKernel.Configuration;
using SharedKernel.Observability;

var builder = WebApplication.CreateBuilder(args);
builder.AddCampusSerilog("AnalyticsService");

var databaseOptions = builder.Configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
var rabbitMqOptions = builder.Configuration.GetSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>() ?? new RabbitMqOptions();

builder.Services.AddSingleton(rabbitMqOptions);
builder.Services.AddSingleton(NpgsqlDataSource.Create(databaseOptions.ConnectionString));
builder.Services.AddSingleton<EventProjector>();
builder.Services.AddSingleton<EcosystemMonitor>();
builder.Services.AddHttpClient();
builder.Services.AddHostedService<AnalyticsEventConsumer>();
builder.Services.AddOpenApi();

builder.Services
    .AddHealthChecks()
    .AddCheck(
        "self",
        () => HealthCheckResult.Healthy(
            "AnalyticsService está disponible."));

var app = builder.Build();
app.UseCorrelationId();
app.UseCampusRequestLogging();

await InitializeDatabaseAsync(app);

app.MapHealthChecks(
    "/api/analytics/health",
    new HealthCheckOptions
    {
        ResponseWriter = (context, report) =>
            HealthCheckResponseWriter.WriteAsync(
                context,
                report,
                "AnalyticsService")
    });

app.MapAnalyticsEndpoints();
app.MapOpenApi();

app.Run();

// PostgreSQL puede tardar en estar listo cuando el ecosistema arranca completo.
static async Task InitializeDatabaseAsync(WebApplication app)
{
    var dataSource = app.Services.GetRequiredService<NpgsqlDataSource>();
    var logger = app.Services.GetRequiredService<ILogger<Program>>();

    for (var attempt = 1; ; attempt++)
    {
        try
        {
            await AnalyticsDatabase.InitializeAsync(dataSource, CancellationToken.None);
            logger.LogInformation("Tablas de lectura del esquema analytics verificadas");
            return;
        }
        catch (Exception ex) when (attempt < 10)
        {
            logger.LogWarning(ex, "PostgreSQL aún no disponible (intento {Attempt}/10)", attempt);
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }
}
