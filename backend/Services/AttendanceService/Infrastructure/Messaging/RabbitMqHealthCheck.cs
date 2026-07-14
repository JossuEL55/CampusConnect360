using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AttendanceService.Infrastructure.Messaging;

public sealed class RabbitMqHealthCheck(
    IRabbitMqConnection rabbitMqConnection) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = await rabbitMqConnection.GetConnectionAsync(
                cancellationToken);

            return connection.IsOpen
                ? HealthCheckResult.Healthy(
                    "RabbitMQ connection is open.")
                : HealthCheckResult.Unhealthy(
                    "RabbitMQ connection is closed.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "RabbitMQ is unavailable.",
                exception);
        }
    }
}
