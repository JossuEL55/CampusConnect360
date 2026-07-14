using System.Text.Json;
using AttendanceService.Application.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using SharedKernel.Events;
using SharedKernel.Messaging;

namespace AttendanceService.Infrastructure.Messaging;

public sealed class StudentEnrolledConsumer(
    IRabbitMqConnection rabbitMqConnection,
    IServiceScopeFactory scopeFactory,
    ILogger<StudentEnrolledConsumer> logger) : BackgroundService
{
    private const ushort PrefetchCount = 10;
    private static readonly TimeSpan RestartDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RequeueDelay = TimeSpan.FromSeconds(2);
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "StudentEnrolled consumer starting for queue {QueueName}. " +
            "ServiceName={ServiceName}",
            RabbitMqNames.Queues.AttendanceStudentEnrolled,
            "AttendanceService");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeUntilStoppedAsync(stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "StudentEnrolled consumer channel stopped unexpectedly. " +
                    "It will restart in {RestartDelaySeconds} seconds. " +
                    "ServiceName={ServiceName}",
                    RestartDelay.TotalSeconds,
                    "AttendanceService");

                await Task.Delay(RestartDelay, stoppingToken);
            }
        }
    }

    private async Task ConsumeUntilStoppedAsync(
        CancellationToken cancellationToken)
    {
        var connection = await rabbitMqConnection.GetConnectionAsync(
            cancellationToken);

        await using var channel = await connection.CreateChannelAsync(
            cancellationToken: cancellationToken);

        await channel.QueueDeclarePassiveAsync(
            RabbitMqNames.Queues.AttendanceStudentEnrolled,
            cancellationToken);

        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: PrefetchCount,
            global: false,
            cancellationToken);

        var channelStopped = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ShutdownAsync += (_, _) =>
        {
            channelStopped.TrySetResult();
            return Task.CompletedTask;
        };

        consumer.ReceivedAsync += (_, delivery) =>
            HandleDeliveryAsync(channel, delivery, cancellationToken);

        var consumerTag = await channel.BasicConsumeAsync(
            RabbitMqNames.Queues.AttendanceStudentEnrolled,
            autoAck: false,
            consumer,
            cancellationToken);

        logger.LogInformation(
            "StudentEnrolled consumer started. Queue={QueueName} " +
            "ConsumerTag={ConsumerTag} PrefetchCount={PrefetchCount} " +
            "ServiceName={ServiceName}",
            RabbitMqNames.Queues.AttendanceStudentEnrolled,
            consumerTag,
            PrefetchCount,
            "AttendanceService");

        await channelStopped.Task.WaitAsync(cancellationToken);
    }

    private async Task HandleDeliveryAsync(
        IChannel channel,
        BasicDeliverEventArgs delivery,
        CancellationToken stoppingToken)
    {
        EventEnvelope<StudentEnrolledData>? envelope = null;

        try
        {
            if (delivery.Exchange != RabbitMqNames.Exchanges.Events ||
                delivery.RoutingKey != RoutingKeys.StudentEnrolled)
            {
                throw new InvalidStudentEnrolledEventException(
                [
                    "Message exchange or routing key does not match " +
                    "the StudentEnrolled topology."
                ]);
            }

            var body = delivery.Body.ToArray();
            envelope = JsonSerializer.Deserialize<
                EventEnvelope<StudentEnrolledData>>(
                body,
                SerializerOptions);

            if (envelope is null)
            {
                throw new JsonException(
                    "The message body did not contain an event envelope.");
            }

            logger.LogInformation(
                "StudentEnrolled event received. EventId={EventId} " +
                "EventType={EventType} CorrelationId={CorrelationId} " +
                "StudentId={StudentId} Redelivered={Redelivered} " +
                "ServiceName={ServiceName}",
                envelope.EventId,
                envelope.EventType,
                envelope.CorrelationId,
                envelope.Data?.StudentId,
                delivery.Redelivered,
                "AttendanceService");

            await using var scope = scopeFactory.CreateAsyncScope();
            var projectionService = scope.ServiceProvider
                .GetRequiredService<IStudentEnrollmentProjectionService>();

            var result = await projectionService.ProjectAsync(
                envelope,
                stoppingToken);

            await channel.BasicAckAsync(
                delivery.DeliveryTag,
                multiple: false,
                CancellationToken.None);

            logger.LogInformation(
                "StudentEnrolled event acknowledged. EventId={EventId} " +
                "EventType={EventType} CorrelationId={CorrelationId} " +
                "StudentId={StudentId} Outcome={ProjectionOutcome} " +
                "ServiceName={ServiceName}",
                envelope.EventId,
                envelope.EventType,
                envelope.CorrelationId,
                result.StudentId,
                result.Outcome,
                "AttendanceService");
        }
        catch (JsonException exception)
        {
            logger.LogWarning(
                exception,
                "Invalid StudentEnrolled JSON rejected without requeue. " +
                "ServiceName={ServiceName}",
                "AttendanceService");

            await NackAsync(channel, delivery, requeue: false);
        }
        catch (InvalidStudentEnrolledEventException exception)
        {
            logger.LogWarning(
                exception,
                "Invalid StudentEnrolled contract rejected. EventId={EventId} " +
                "EventType={EventType} CorrelationId={CorrelationId} " +
                "StudentId={StudentId} ValidationErrors={ValidationErrors} " +
                "ServiceName={ServiceName}",
                envelope?.EventId,
                envelope?.EventType,
                envelope?.CorrelationId,
                envelope?.Data?.StudentId,
                exception.Errors,
                "AttendanceService");

            await NackAsync(channel, delivery, requeue: false);
        }
        catch (StudentEnrollmentConflictException exception)
        {
            logger.LogError(
                exception,
                "Student enrollment conflict rejected without requeue. " +
                "EventId={EventId} CorrelationId={CorrelationId} " +
                "StudentId={StudentId} EnrollmentId={EnrollmentId} " +
                "ExistingStudentId={ExistingStudentId} " +
                "ServiceName={ServiceName}",
                envelope?.EventId,
                envelope?.CorrelationId,
                exception.ReceivedStudentId,
                exception.EnrollmentId,
                exception.ExistingStudentId,
                "AttendanceService");

            await NackAsync(channel, delivery, requeue: false);
        }
        catch (Exception exception) when (IsTransient(exception))
        {
            logger.LogError(
                exception,
                "Transient StudentEnrolled processing error. " +
                "Message will be requeued after a short delay. " +
                "EventId={EventId} EventType={EventType} " +
                "CorrelationId={CorrelationId} StudentId={StudentId} " +
                "ServiceName={ServiceName}",
                envelope?.EventId,
                envelope?.EventType,
                envelope?.CorrelationId,
                envelope?.Data?.StudentId,
                "AttendanceService");

            await Task.Delay(RequeueDelay, CancellationToken.None);
            await NackAsync(channel, delivery, requeue: true);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Unexpected StudentEnrolled processing error rejected " +
                "without requeue. EventId={EventId} " +
                "CorrelationId={CorrelationId} StudentId={StudentId} " +
                "ServiceName={ServiceName}",
                envelope?.EventId,
                envelope?.CorrelationId,
                envelope?.Data?.StudentId,
                "AttendanceService");

            await NackAsync(channel, delivery, requeue: false);
        }
    }

    private async Task NackAsync(
        IChannel channel,
        BasicDeliverEventArgs delivery,
        bool requeue)
    {
        await channel.BasicNackAsync(
            delivery.DeliveryTag,
            multiple: false,
            requeue,
            CancellationToken.None);

        logger.LogWarning(
            "StudentEnrolled message negatively acknowledged. " +
            "DeliveryTag={DeliveryTag} Requeue={Requeue} " +
            "ServiceName={ServiceName}",
            delivery.DeliveryTag,
            requeue,
            "AttendanceService");
    }

    private static bool IsTransient(Exception exception) =>
        exception is NpgsqlException or
            DbUpdateException or
            TimeoutException or
            IOException or
            BrokerUnreachableException or
            AlreadyClosedException or
            OperationInterruptedException;
}
