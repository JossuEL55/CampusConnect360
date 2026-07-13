using System.Text.Json;
using AnalyticsService.Projections;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SharedKernel.Configuration;
using SharedKernel.Events;
using SharedKernel.Messaging;

namespace AnalyticsService.Messaging;

// Consume la cola analytics.all-events (binding #) y delega cada evento al proyector.
public sealed class AnalyticsEventConsumer(
    RabbitMqOptions options,
    EventProjector projector,
    ILogger<AnalyticsEventConsumer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNameCaseInsensitive = true };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "RabbitMQ no disponible; se reintenta la conexión en 5 segundos");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task ConsumeAsync(CancellationToken ct)
    {
        var factory = new ConnectionFactory
        {
            HostName = options.HostName,
            Port = options.Port,
            UserName = options.UserName,
            Password = options.Password,
            VirtualHost = options.VirtualHost
        };

        await using var connection = await factory.CreateConnectionAsync(ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);
        await channel.BasicQosAsync(0, 20, false, ct);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, delivery) => HandleAsync(channel, delivery, ct);

        await channel.BasicConsumeAsync(RabbitMqNames.Queues.AnalyticsAllEvents, autoAck: false, consumer, ct);
        logger.LogInformation("Consumiendo la cola {Queue}", RabbitMqNames.Queues.AnalyticsAllEvents);

        await Task.Delay(Timeout.Infinite, ct);
    }

    private async Task HandleAsync(IChannel channel, BasicDeliverEventArgs delivery, CancellationToken ct)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<EventEnvelope<JsonElement>>(delivery.Body.Span, SerializerOptions)
                ?? throw new JsonException("Mensaje vacío");

            var applied = await projector.ApplyAsync(envelope, ct);
            if (applied)
            {
                logger.LogInformation(
                    "Evento proyectado {EventType} {EventId} (correlación {CorrelationId})",
                    envelope.EventType, envelope.EventId, envelope.CorrelationId);
            }

            await channel.BasicAckAsync(delivery.DeliveryTag, false, ct);
        }
        catch (JsonException ex)
        {
            // La cola no tiene DLX configurado: el mensaje inválido se registra y se descarta para no bloquearla.
            logger.LogError(ex, "Mensaje con formato inválido descartado (routing key {RoutingKey})", delivery.RoutingKey);
            await channel.BasicAckAsync(delivery.DeliveryTag, false, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al proyectar; el mensaje se reencola (routing key {RoutingKey})", delivery.RoutingKey);
            await Task.Delay(TimeSpan.FromSeconds(1), ct);
            await channel.BasicNackAsync(delivery.DeliveryTag, false, requeue: true, ct);
        }
    }
}
