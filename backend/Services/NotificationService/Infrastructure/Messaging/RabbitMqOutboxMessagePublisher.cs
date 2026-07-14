using System.Text;
using NotificationService.Domain.Entities;
using RabbitMQ.Client;
using SharedKernel.Messaging;

namespace NotificationService.Infrastructure.Messaging;

public sealed class RabbitMqOutboxMessagePublisher(
    IRabbitMqConnection connection,
    ILogger<RabbitMqOutboxMessagePublisher> logger) :
    IOutboxMessagePublisher,
    IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IChannel? _channel;

    public async Task PublishAsync(
        OutboxMessage message,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var channel = await GetChannelAsync(cancellationToken);
            var properties = new BasicProperties
            {
                ContentType = "application/json",
                ContentEncoding = "utf-8",
                DeliveryMode = DeliveryModes.Persistent,
                MessageId = message.EventId,
                CorrelationId = message.CorrelationId,
                Type = message.EventType,
                AppId = "NotificationService",
                Timestamp = new AmqpTimestamp(message.OccurredAt.ToUnixTimeSeconds())
            };
            await channel.BasicPublishAsync(
                RabbitMqNames.Exchanges.Events,
                message.RoutingKey,
                mandatory: true,
                properties,
                Encoding.UTF8.GetBytes(message.Payload),
                cancellationToken);
            logger.LogInformation(
                "Notification outbox publisher confirmation received. " +
                "EventId={EventId} EventType={EventType} RoutingKey={RoutingKey} " +
                "CorrelationId={CorrelationId} ServiceName={ServiceName}",
                message.EventId,
                message.EventType,
                message.RoutingKey,
                message.CorrelationId,
                "NotificationService");
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true }) return _channel;
        if (_channel is not null) await _channel.DisposeAsync();
        var activeConnection = await connection.GetConnectionAsync(cancellationToken);
        _channel = await activeConnection.CreateChannelAsync(
            new CreateChannelOptions(true, true),
            cancellationToken);
        return _channel;
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null) await _channel.DisposeAsync();
        _gate.Dispose();
    }
}
