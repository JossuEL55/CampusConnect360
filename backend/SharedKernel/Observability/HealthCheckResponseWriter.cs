using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SharedKernel.Observability;

// Genera una respuesta JSON uniforme para los health checks del ecosistema.
public static class HealthCheckResponseWriter
{
    public static async Task WriteAsync(
        HttpContext context,
        HealthReport report,
        string serviceName)
    {
        context.Response.ContentType = "application/json";

        var correlationId =
            context.Items[
                CorrelationConstants.LogPropertyName
            ]?.ToString();

        var response = new
        {
            service = serviceName,
            status = report.Status.ToString(),
            correlationId,
            timestamp = DateTimeOffset.UtcNow,
            durationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                durationMs =
                    entry.Value.Duration.TotalMilliseconds
            })
        };

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(
                response,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
    }
}