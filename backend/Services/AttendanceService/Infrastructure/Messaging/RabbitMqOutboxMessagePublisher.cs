using System.Text;
using AttendanceService.Domain.Entities;
using RabbitMQ.Client;
using SharedKernel.Messaging;

namespace AttendanceService.Infrastructure.Messaging;

public sealed class RabbitMqOutboxMessagePublisher(
    IRabbitMqConnection rabbitMqConnection,
    ILogger<RabbitMqOutboxMessagePublisher> logger) :
    IOutboxMessagePublisher,
    IAsyncDisposable
{
    private readonly SemaphoreSlim _publishLock = new(1, 1);
    private IChannel? _channel;

    public async Task PublishAsync(
        OutboxMessage message,
        CancellationToken cancellationToken)
    {
        await _publishLock.WaitAsync(cancellationToken);
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
                AppId = "AttendanceService",
                Timestamp = new AmqpTimestamp(
                    message.OccurredAt.ToUnixTimeSeconds())
            };

            await channel.BasicPublishAsync(
                RabbitMqNames.Exchanges.Events,
                message.RoutingKey,
                mandatory: true,
                properties,
                Encoding.UTF8.GetBytes(message.Payload),
                cancellationToken);

            logger.LogInformation(
                "RabbitMQ publisher confirmation received. " +
                "EventId={EventId} EventType={EventType} " +
                "RoutingKey={RoutingKey} CorrelationId={CorrelationId} " +
                "ServiceName={ServiceName}",
                message.EventId,
                message.EventType,
                message.RoutingKey,
                message.CorrelationId,
                "AttendanceService");
        }
        finally
        {
            _publishLock.Release();
        }
    }

    private async Task<IChannel> GetChannelAsync(
        CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        if (_channel is not null)
        {
            await _channel.DisposeAsync();
        }

        var connection = await rabbitMqConnection.GetConnectionAsync(
            cancellationToken);
        var options = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);
        _channel = await connection.CreateChannelAsync(
            options,
            cancellationToken);

        return _channel;
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
        }

        _publishLock.Dispose();
    }
}
