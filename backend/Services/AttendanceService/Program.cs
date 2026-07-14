using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using AttendanceService.Application.Services;
using AttendanceService.Endpoints;
using AttendanceService.Infrastructure.Development;
using AttendanceService.Infrastructure.Errors;
using AttendanceService.Infrastructure.Persistence;
using SharedKernel.Configuration;
using SharedKernel.Observability;

var builder = WebApplication.CreateBuilder(args);
builder.AddCampusSerilog("AttendanceService");

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        var correlationId = context.HttpContext.Items[
            CorrelationConstants.LogPropertyName]?.ToString();

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            context.ProblemDetails.Extensions["correlationId"] =
                correlationId;
        }
    };
});
builder.Services.AddExceptionHandler<BadRequestExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddScoped<IAttendanceApplicationService,
    AttendanceApplicationService>();
builder.Services.AddScoped<DevelopmentDataSeeder>();
builder.Services.AddSingleton(TimeProvider.System);

var databaseSection = builder.Configuration.GetSection(
    DatabaseOptions.SectionName);

builder.Services
    .AddOptions<DatabaseOptions>()
    .Bind(databaseSection)
    .Validate(
        options =>
            !string.IsNullOrWhiteSpace(options.Host) &&
            options.Port is > 0 and <= 65535 &&
            !string.IsNullOrWhiteSpace(options.Name) &&
            !string.IsNullOrWhiteSpace(options.UserName) &&
            !string.IsNullOrWhiteSpace(options.Password) &&
            options.Schema == DatabaseSchemas.Attendance,
        "Database configuration must be complete and use the attendance schema.")
    .ValidateOnStart();

var databaseOptions = databaseSection.Get<DatabaseOptions>()
    ?? throw new InvalidOperationException(
        "Database configuration was not found.");

builder.Services.AddDbContext<AttendanceDbContext>(options =>
    options.UseNpgsql(
        databaseOptions.ConnectionString,
        npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly(
                typeof(AttendanceDbContext).Assembly.FullName);
            npgsqlOptions.MigrationsHistoryTable(
                "__ef_migrations_history",
                DatabaseSchemas.Attendance);
        }));

builder.Services
    .AddHealthChecks()
    .AddCheck(
        "self",
        () => HealthCheckResult.Healthy(
            "AttendanceService está disponible."));

builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<AttendanceDbContext>(
        "postgresql",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["database", "postgresql"]);

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCorrelationId();
app.UseCampusRequestLogging();

app.MapHealthChecks(
    "/api/attendance/health",
    new HealthCheckOptions
    {
        ResponseWriter = (context, report) =>
            HealthCheckResponseWriter.WriteAsync(
                context,
                report,
                "AttendanceService")
    });

app.MapAttendanceEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    await using var scope = app.Services.CreateAsyncScope();
    var seeder = scope.ServiceProvider
        .GetRequiredService<DevelopmentDataSeeder>();
    await seeder.SeedAsync();
}

app.Run();
