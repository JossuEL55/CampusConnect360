using System.Text;
using NotificationService.Domain.Entities;
using RabbitMQ.Client;
using SharedKernel.Messaging;

namespace NotificationService.Infrastructure.Messaging;

public sealed class RabbitMqDeadLetterPublisher(
    IRabbitMqConnection connection,
    ILogger<RabbitMqDeadLetterPublisher> logger) : IDeadLetterPublisher, IAsyncDisposable
{
    public const string RoutingKey = "notifications.dead-letter";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IChannel? _channel;

    public async Task PublishAsync(FailedMessage message, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var channel = await GetChannelAsync(cancellationToken);
            var properties = CreateProperties(message);
            await channel.BasicPublishAsync(RabbitMqNames.Exchanges.DeadLetter,
                RoutingKey, mandatory: true, properties,
                Encoding.UTF8.GetBytes(message.OriginalPayload), cancellationToken);
            logger.LogWarning(
                "Failed notification message published to DLQ. FailedMessageId={FailedMessageId} NotificationId={NotificationId} Exchange={Exchange} RoutingKey={RoutingKey} Attempts={Attempts} ServiceName={ServiceName}",
                message.Id, message.NotificationId, RabbitMqNames.Exchanges.DeadLetter,
                RoutingKey, message.Attempts, "NotificationService");
        }
        finally { _gate.Release(); }
    }

    public static BasicProperties CreateProperties(FailedMessage message) => new()
    {
        ContentType = "application/json", ContentEncoding = "utf-8",
        DeliveryMode = DeliveryModes.Persistent,
        MessageId = message.Id.ToString("D"),
        CorrelationId = message.CorrelationId,
        Type = "NotificationDeliveryFailed",
        AppId = "NotificationService",
        Timestamp = new AmqpTimestamp(message.FailedAt.ToUnixTimeSeconds()),
        Headers = new Dictionary<string, object?>
        {
            ["x-death-reason"] = message.FailureReason,
            ["x-original-event-id"] = message.SourceEventId,
            ["x-notification-id"] = message.NotificationId.ToString("D"),
            ["x-retry-count"] = message.Attempts
        }
    };

    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true }) return _channel;
        if (_channel is not null) await _channel.DisposeAsync();
        var active = await connection.GetConnectionAsync(cancellationToken);
        _channel = await active.CreateChannelAsync(new CreateChannelOptions(true, true), cancellationToken);
        return _channel;
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null) await _channel.DisposeAsync();
        _gate.Dispose();
    }
}
