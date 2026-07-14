using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using PaymentService.Api;
using PaymentService.Application;
using PaymentService.Domain;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SharedKernel.Configuration;
using SharedKernel.Events;
using SharedKernel.Messaging;

namespace PaymentService.Messaging;

public interface IIntegrationEventPublisher
{
    Task PublishAsync(OutboxMessage message, CancellationToken ct);
}

public sealed class RabbitMqEventBus(
    IOptions<RabbitMqOptions> options,
    IServiceScopeFactory scopeFactory,
    ILogger<RabbitMqEventBus> logger) : BackgroundService, IIntegrationEventPublisher
{
    private readonly SemaphoreSlim connectionGate = new(1, 1);
    private readonly SemaphoreSlim publishGate = new(1, 1);
    private IConnection? connection;
    private IChannel? publishChannel;
    private IChannel? consumerChannel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var activeConnection = await EnsureConnectionAsync(stoppingToken);
                consumerChannel = await activeConnection.CreateChannelAsync(cancellationToken: stoppingToken);
                await consumerChannel.BasicQosAsync(0, 1, false, stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(consumerChannel);
                consumer.ReceivedAsync += (_, args) => ProcessStudentEnrolledAsync(args, stoppingToken);
                await consumerChannel.BasicConsumeAsync(
                    RabbitMqNames.Queues.PaymentsStudentEnrolled,
                    autoAck: false,
                    consumer,
                    stoppingToken);

                logger.LogInformation("Consumiendo la cola {Queue}.", RabbitMqNames.Queues.PaymentsStudentEnrolled);
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "No se pudo iniciar el consumidor RabbitMQ; se reintentará en 5 segundos.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    public async Task PublishAsync(OutboxMessage message, CancellationToken ct)
    {
        await publishGate.WaitAsync(ct);
        try
        {
            var channel = await EnsurePublishChannelAsync(ct);
            var properties = new BasicProperties
            {
                ContentType = "application/json",
                ContentEncoding = "utf-8",
                DeliveryMode = DeliveryModes.Persistent,
                MessageId = message.EventId.ToString(),
                CorrelationId = message.CorrelationId,
                Type = message.EventType,
                AppId = "PaymentService",
                Timestamp = new AmqpTimestamp(message.OccurredAt.ToUnixTimeSeconds()),
                Headers = new Dictionary<string, object?> { ["x-retry-count"] = 0 }
            };
            var body = System.Text.Encoding.UTF8.GetBytes(message.Payload);
            await channel.BasicPublishAsync(
                RabbitMqNames.Exchanges.Events,
                message.RoutingKey,
                mandatory: false,
                properties,
                body,
                ct);
        }
        finally
        {
            publishGate.Release();
        }
    }

    private async Task ProcessStudentEnrolledAsync(
        BasicDeliverEventArgs args, CancellationToken stoppingToken)
    {
        if (consumerChannel is null) return;
        try
        {
            var envelope = JsonSerializer.Deserialize<EventEnvelope<StudentEnrolledData>>(
                args.Body.Span,
                JsonOptions) ?? throw new JsonException("Evento StudentEnrolled vacío.");
            ValidateStudentEnrolled(envelope);

            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<PaymentOperations>()
                .StudentEnrolledAsync(envelope, stoppingToken);
            await consumerChannel.BasicAckAsync(args.DeliveryTag, false, stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "StudentEnrolled inválido o no procesable; se enviará al Dead Letter Exchange.");
            await consumerChannel.BasicNackAsync(
                args.DeliveryTag,
                multiple: false,
                requeue: false,
                stoppingToken);
        }
    }

    private async Task<IChannel> EnsurePublishChannelAsync(CancellationToken ct)
    {
        if (publishChannel is { IsOpen: true }) return publishChannel;
        var activeConnection = await EnsureConnectionAsync(ct);
        publishChannel = await activeConnection.CreateChannelAsync(cancellationToken: ct);
        return publishChannel;
    }

    private async Task<IConnection> EnsureConnectionAsync(CancellationToken ct)
    {
        if (connection is { IsOpen: true }) return connection;
        await connectionGate.WaitAsync(ct);
        try
        {
            if (connection is { IsOpen: true }) return connection;
            var config = options.Value;
            var factory = new ConnectionFactory
            {
                HostName = config.HostName,
                Port = config.Port,
                UserName = config.UserName,
                Password = config.Password,
                VirtualHost = config.VirtualHost,
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true
            };
            connection = await factory.CreateConnectionAsync("payment-service", ct);
            return connection;
        }
        finally
        {
            connectionGate.Release();
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (consumerChannel is not null) await consumerChannel.DisposeAsync();
        if (publishChannel is not null) await publishChannel.DisposeAsync();
        if (connection is not null) await connection.DisposeAsync();
        await base.StopAsync(cancellationToken);
    }

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static void ValidateStudentEnrolled(EventEnvelope<StudentEnrolledData> envelope)
    {
        if (envelope.EventId == Guid.Empty ||
            envelope.EventType != EventTypes.StudentEnrolled ||
            string.IsNullOrWhiteSpace(envelope.CorrelationId) ||
            envelope.Data.StudentId == Guid.Empty ||
            envelope.Data.EnrollmentId == Guid.Empty ||
            string.IsNullOrWhiteSpace(envelope.Data.StudentCode) ||
            string.IsNullOrWhiteSpace(envelope.Data.FullName) ||
            string.IsNullOrWhiteSpace(envelope.Data.SchoolYear))
        {
            throw new JsonException("El evento StudentEnrolled no cumple el contrato de integración.");
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };
        jsonOptions.Converters.Add(new JsonStringEnumConverter());
        return jsonOptions;
    }
}
