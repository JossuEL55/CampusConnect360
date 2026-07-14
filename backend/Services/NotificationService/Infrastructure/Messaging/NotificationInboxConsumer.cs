using Microsoft.EntityFrameworkCore;
using Npgsql;
using NotificationService.Application.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using SharedKernel.Messaging;

namespace NotificationService.Infrastructure.Messaging;

public sealed class NotificationInboxConsumer(
    IRabbitMqConnection rabbitMqConnection,
    IServiceScopeFactory scopeFactory,
    ILogger<NotificationInboxConsumer> logger) : BackgroundService
{
    private const ushort PrefetchCount = 10;
    private static readonly TimeSpan RestartDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RequeueDelay = TimeSpan.FromSeconds(2);

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
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Notification inbox consumer stopped; restarting in " +
                    "{DelaySeconds} seconds. ServiceName={ServiceName}",
                    RestartDelay.TotalSeconds,
                    "NotificationService");
                await Task.Delay(RestartDelay, stoppingToken);
            }
        }
    }

    private async Task ConsumeAsync(CancellationToken cancellationToken)
    {
        var connection = await rabbitMqConnection.GetConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(
            cancellationToken: cancellationToken);
        await channel.QueueDeclarePassiveAsync(
            RabbitMqNames.Queues.NotificationsInbox,
            cancellationToken);
        await channel.BasicQosAsync(0, PrefetchCount, false, cancellationToken);

        var stopped = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ShutdownAsync += (_, _) =>
        {
            stopped.TrySetResult();
            return Task.CompletedTask;
        };
        consumer.ReceivedAsync += (_, delivery) =>
            HandleAsync(channel, delivery, cancellationToken);
        var tag = await channel.BasicConsumeAsync(
            RabbitMqNames.Queues.NotificationsInbox,
            autoAck: false,
            consumer,
            cancellationToken);

        logger.LogInformation(
            "Notification inbox consumer started. Queue={QueueName} " +
            "ConsumerTag={ConsumerTag} PrefetchCount={PrefetchCount} " +
            "ServiceName={ServiceName}",
            RabbitMqNames.Queues.NotificationsInbox,
            tag,
            PrefetchCount,
            "NotificationService");
        await stopped.Task.WaitAsync(cancellationToken);
    }

    private async Task HandleAsync(
        IChannel channel,
        BasicDeliverEventArgs delivery,
        CancellationToken cancellationToken)
    {
        try
        {
            if (delivery.Exchange != RabbitMqNames.Exchanges.Events)
            {
                throw new InvalidNotificationEventException(
                    "Unexpected RabbitMQ exchange.");
            }

            var body = delivery.Body.ToArray();
            await using var scope = scopeFactory.CreateAsyncScope();
            var processor = scope.ServiceProvider
                .GetRequiredService<NotificationEventProcessor>();
            var result = await processor.ProcessAsync(body, cancellationToken);
            await channel.BasicAckAsync(
                delivery.DeliveryTag,
                false,
                CancellationToken.None);
            logger.LogInformation(
                "Notification source event acknowledged. RoutingKey={RoutingKey} " +
                "Outcome={Outcome} Redelivered={Redelivered} " +
                "ServiceName={ServiceName}",
                delivery.RoutingKey,
                result.Outcome,
                delivery.Redelivered,
                "NotificationService");
        }
        catch (InvalidNotificationEventException exception)
        {
            logger.LogWarning(
                exception,
                "Invalid notification source event rejected. " +
                "RoutingKey={RoutingKey} Requeue={Requeue} " +
                "ServiceName={ServiceName}",
                delivery.RoutingKey,
                false,
                "NotificationService");
            await NackAsync(channel, delivery, false);
        }
        catch (StudentReplicaNotFoundException exception)
        {
            logger.LogWarning(
                exception,
                "Student replica is missing; event will be requeued. " +
                "StudentId={StudentId} RoutingKey={RoutingKey} " +
                "ServiceName={ServiceName}",
                exception.StudentId,
                delivery.RoutingKey,
                "NotificationService");
            await Task.Delay(RequeueDelay, CancellationToken.None);
            await NackAsync(channel, delivery, true);
        }
        catch (Exception exception) when (IsTransient(exception))
        {
            logger.LogError(
                exception,
                "Transient notification processing error; event will be requeued. " +
                "RoutingKey={RoutingKey} ServiceName={ServiceName}",
                delivery.RoutingKey,
                "NotificationService");
            await Task.Delay(RequeueDelay, CancellationToken.None);
            await NackAsync(channel, delivery, true);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Unexpected notification processing error rejected without requeue. " +
                "RoutingKey={RoutingKey} ServiceName={ServiceName}",
                delivery.RoutingKey,
                "NotificationService");
            await NackAsync(channel, delivery, false);
        }
    }

    private static Task NackAsync(
        IChannel channel,
        BasicDeliverEventArgs delivery,
        bool requeue) => channel.BasicNackAsync(
            delivery.DeliveryTag,
            false,
            requeue,
            CancellationToken.None).AsTask();

    private static bool IsTransient(Exception exception) =>
        exception is NpgsqlException or DbUpdateException or TimeoutException or
            IOException or BrokerUnreachableException or AlreadyClosedException or
            OperationInterruptedException;
}
