using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AnalyticsService.Monitoring;

public sealed record EcosystemSnapshot(
    string Status,
    bool BrokerUp,
    int DlqDepth,
    IReadOnlyList<ServiceHealth> Services);

public sealed record ServiceHealth(string Name, string Status);

// Consulta los health checks de los servicios y la profundidad de la DLQ en RabbitMQ.
// Reglas del contrato (9.2): Healthy = todo OK y DLQ vacía; Degraded = un health fallido
// o mensajes en DLQ; Down = broker caído o más de un servicio caído.
public sealed class EcosystemMonitor(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<EcosystemMonitor> logger)
{
    public async Task<EcosystemSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(3);

        var healthUrls = configuration.GetSection("HealthServices").GetChildren()
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Value))
            .ToDictionary(entry => entry.Key, entry => entry.Value!);

        var checks = healthUrls.Select(async entry =>
        {
            try
            {
                var response = await client.GetAsync(entry.Value, cancellationToken);
                return new ServiceHealth(entry.Key, response.IsSuccessStatusCode ? "Healthy" : "Unhealthy");
            }
            catch (Exception)
            {
                return new ServiceHealth(entry.Key, "Unreachable");
            }
        });
        var services = await Task.WhenAll(checks);

        var (brokerUp, dlqDepth) = await GetDlqDepthAsync(client, cancellationToken);

        var downCount = services.Count(service => service.Status != "Healthy");
        var status = !brokerUp || downCount > 1 ? "Down"
            : downCount == 1 || dlqDepth > 0 ? "Degraded"
            : "Healthy";

        return new EcosystemSnapshot(status, brokerUp, dlqDepth, services);
    }

    private async Task<(bool BrokerUp, int DlqDepth)> GetDlqDepthAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var baseUrl = configuration["RabbitMqApi:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return (true, 0);
        }

        try
        {
            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(
                $"{configuration["RabbitMqApi:UserName"]}:{configuration["RabbitMqApi:Password"]}"));

            using var request = new HttpRequestMessage(
                HttpMethod.Get, $"{baseUrl.TrimEnd('/')}/api/queues/%2F/notifications.dlq");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return (response.StatusCode != System.Net.HttpStatusCode.ServiceUnavailable, 0);
            }

            using var queue = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var depth = queue.RootElement.TryGetProperty("messages", out var messages) ? messages.GetInt32() : 0;
            return (true, depth);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No fue posible consultar la API de RabbitMQ");
            return (false, 0);
        }
    }
}
