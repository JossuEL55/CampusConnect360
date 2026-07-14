using AcademicService.Api;
using AcademicService.Application;
using AcademicService.Infrastructure;
using AcademicService.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json.Serialization;
using SharedKernel.Configuration;
using SharedKernel.Observability;

var builder = WebApplication.CreateBuilder(args);
builder.AddCampusSerilog("AcademicService");

builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));

var database = builder.Configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new();
builder.Services.AddDbContext<AcademicDbContext>(options => options.UseNpgsql(database.ConnectionString));
builder.Services.AddScoped<AcademicOperations>();
builder.Services.AddSingleton<RabbitMqEventBus>();
builder.Services.AddSingleton<IIntegrationEventPublisher>(sp => sp.GetRequiredService<RabbitMqEventBus>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<RabbitMqEventBus>());
builder.Services.AddHostedService<OutboxPublisherWorker>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks().AddCheck("self", () => HealthCheckResult.Healthy("AcademicService está disponible."));

var app = builder.Build();
app.UseCorrelationId();
app.UseCampusRequestLogging();
app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "AcademicService v1");
    options.RoutePrefix = "swagger";
});

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AcademicDbContext>();
    await db.Database.MigrateAsync();
}

app.MapGet("/api/academic/health", async (
    HttpContext context,
    HealthCheckService healthCheckService) =>
{
    var report = await healthCheckService.CheckHealthAsync(context.RequestAborted);
    context.Response.StatusCode = report.Status == HealthStatus.Healthy
        ? StatusCodes.Status200OK
        : StatusCodes.Status503ServiceUnavailable;
    await HealthCheckResponseWriter.WriteAsync(context, report, "AcademicService");
})
.WithName("AcademicHealth")
.WithTags("Health")
.WithSummary("Comprueba el estado del servicio académico")
.WithDescription("Retorna el estado de disponibilidad de AcademicService.")
.Produces(StatusCodes.Status200OK, contentType: "application/json")
.Produces(StatusCodes.Status503ServiceUnavailable, contentType: "application/json");
app.MapAcademicEndpoints();
app.Run();

public partial class Program;
