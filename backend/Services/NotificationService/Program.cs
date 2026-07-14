using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NotificationService.Application.Services;
using NotificationService.Endpoints;
using NotificationService.Infrastructure.Messaging;
using NotificationService.Infrastructure.Persistence;
using SharedKernel.Configuration;
using SharedKernel.Observability;

var builder = WebApplication.CreateBuilder(args);
builder.AddCampusSerilog("NotificationService");

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        var correlationId = context.HttpContext.Items[
            CorrelationConstants.LogPropertyName]?.ToString();
        if (!string.IsNullOrWhiteSpace(correlationId))
            context.ProblemDetails.Extensions["correlationId"] = correlationId;
    };
});
builder.Services.AddOpenApi();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.Configure<NotificationOptions>(
    builder.Configuration.GetSection(NotificationOptions.SectionName));
builder.Services.AddSingleton(new NotificationFailureMode(
    builder.Configuration.GetValue<bool>("Notifications:FailureMode")));
builder.Services.AddSingleton<NotificationProcessingCoordinator>();
builder.Services.AddScoped<NotificationEventProcessor>();
builder.Services.AddScoped<NotificationDeliveryProcessor>();
builder.Services.AddScoped<NotificationRetryService>();
builder.Services.AddScoped<NotificationQueryService>();
builder.Services.AddScoped<OutboxProcessor>();
builder.Services.AddScoped<DeadLetterProcessor>();
builder.Services.AddSingleton<IRabbitMqConnection, RabbitMqConnection>();
builder.Services.AddSingleton<IOutboxMessagePublisher,
    RabbitMqOutboxMessagePublisher>();
builder.Services.AddSingleton<IDeadLetterPublisher,
    RabbitMqDeadLetterPublisher>();
builder.Services.AddHostedService<NotificationInboxConsumer>();
builder.Services.AddHostedService<NotificationDeliveryWorker>();
builder.Services.AddHostedService<OutboxPublisherWorker>();
builder.Services.AddHostedService<DeadLetterPublisherWorker>();

var databaseSection = builder.Configuration.GetSection(DatabaseOptions.SectionName);
builder.Services.AddOptions<DatabaseOptions>().Bind(databaseSection)
    .Validate(x => !string.IsNullOrWhiteSpace(x.Host) && x.Port is > 0 and <= 65535 &&
        !string.IsNullOrWhiteSpace(x.Name) && !string.IsNullOrWhiteSpace(x.UserName) &&
        !string.IsNullOrWhiteSpace(x.Password) &&
        x.Schema == DatabaseSchemas.Notifications,
        "Database configuration must use the notifications schema.")
    .ValidateOnStart();
var database = databaseSection.Get<DatabaseOptions>() ??
    throw new InvalidOperationException("Database configuration was not found.");
builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseNpgsql(database.ConnectionString, npgsql =>
    {
        npgsql.MigrationsAssembly(typeof(NotificationDbContext).Assembly.FullName);
        npgsql.MigrationsHistoryTable(
            "__ef_migrations_history",
            DatabaseSchemas.Notifications);
    }));

builder.Services.AddOptions<RabbitMqOptions>()
    .Bind(builder.Configuration.GetSection(RabbitMqOptions.SectionName))
    .Validate(x => !string.IsNullOrWhiteSpace(x.HostName) &&
        x.Port is > 0 and <= 65535 && !string.IsNullOrWhiteSpace(x.UserName) &&
        !string.IsNullOrWhiteSpace(x.Password) && !string.IsNullOrWhiteSpace(x.VirtualHost),
        "RabbitMq configuration must be complete.")
    .ValidateOnStart();

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(
        "NotificationService está disponible."))
    .AddDbContextCheck<NotificationDbContext>(
        "postgresql",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["database", "postgresql"])
    .AddCheck<RabbitMqHealthCheck>(
        "rabbitmq",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["messaging", "rabbitmq"]);

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCorrelationId();
app.UseCampusRequestLogging();
app.MapHealthChecks("/api/notifications/health", new HealthCheckOptions
{
    ResponseWriter = (context, report) =>
        HealthCheckResponseWriter.WriteAsync(context, report, "NotificationService")
});
app.MapNotificationEndpoints();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.Run();
