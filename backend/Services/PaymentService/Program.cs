using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PaymentService.Api;
using PaymentService.Application;
using PaymentService.Infrastructure;
using PaymentService.Messaging;
using SharedKernel.Configuration;
using SharedKernel.Observability;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.AddCampusSerilog("PaymentService");

builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));

var database = builder.Configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new();
builder.Services.AddDbContext<PaymentDbContext>(options => options.UseNpgsql(database.ConnectionString));
builder.Services.AddScoped<PaymentOperations>();
builder.Services.AddSingleton<RabbitMqEventBus>();
builder.Services.AddSingleton<IIntegrationEventPublisher>(sp => sp.GetRequiredService<RabbitMqEventBus>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<RabbitMqEventBus>());
builder.Services.AddHostedService<OutboxPublisherWorker>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services
    .AddHealthChecks()
    .AddCheck(
        "self",
        () => HealthCheckResult.Healthy(
            "PaymentService está disponible."));

var app = builder.Build();
app.UseCorrelationId();
app.UseCampusRequestLogging();
app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "PaymentService v1");
    options.RoutePrefix = "swagger";
});

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
    await db.Database.MigrateAsync();
}

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

app.MapPaymentEndpoints();
app.Run();

public partial class Program;
