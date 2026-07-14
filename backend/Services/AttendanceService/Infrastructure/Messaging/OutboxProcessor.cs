using AttendanceService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AttendanceService.Infrastructure.Messaging;

public sealed class OutboxProcessor(
    AttendanceDbContext dbContext,
    IOutboxMessagePublisher publisher,
    TimeProvider timeProvider,
    ILogger<OutboxProcessor> logger)
{
    public const int BatchSize = 20;
    public const int LastErrorMaximumLength = 2000;

    public async Task<bool> PublishPendingBatchAsync(
        CancellationToken cancellationToken)
    {
        var pending = await dbContext.OutboxMessages
            .Where(message => message.ProcessedAt == null)
            .OrderBy(message => message.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            return false;
        }

        logger.LogInformation(
            "Attendance outbox batch started. MessageCount={MessageCount} " +
            "ServiceName={ServiceName}",
            pending.Count,
            "AttendanceService");

        var publishedAny = false;

        foreach (var message in pending)
        {
            try
            {
                await publisher.PublishAsync(message, cancellationToken);

                logger.LogInformation(
                    "Outbox event published. EventId={EventId} " +
                    "EventType={EventType} RoutingKey={RoutingKey} " +
                    "CorrelationId={CorrelationId} Attempts={Attempts} " +
                    "ServiceName={ServiceName}",
                    message.EventId,
                    message.EventType,
                    message.RoutingKey,
                    message.CorrelationId,
                    message.Attempts,
                    "AttendanceService");

                message.ProcessedAt = timeProvider.GetUtcNow();
                message.LastError = null;
                await dbContext.SaveChangesAsync(cancellationToken);
                publishedAny = true;

                logger.LogInformation(
                    "Outbox event marked as processed. EventId={EventId} " +
                    "EventType={EventType} RoutingKey={RoutingKey} " +
                    "CorrelationId={CorrelationId} ServiceName={ServiceName}",
                    message.EventId,
                    message.EventType,
                    message.RoutingKey,
                    message.CorrelationId,
                    "AttendanceService");
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                message.Attempts++;
                message.LastError = Summarize(exception);
                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogError(
                    exception,
                    "Outbox publication failed. EventId={EventId} " +
                    "EventType={EventType} RoutingKey={RoutingKey} " +
                    "CorrelationId={CorrelationId} Attempts={Attempts} " +
                    "ServiceName={ServiceName}",
                    message.EventId,
                    message.EventType,
                    message.RoutingKey,
                    message.CorrelationId,
                    message.Attempts,
                    "AttendanceService");

                break;
            }
        }

        return publishedAny;
    }

    private static string Summarize(Exception exception)
    {
        var summary = $"{exception.GetType().Name}: {exception.Message}";
        return summary.Length <= LastErrorMaximumLength
            ? summary
            : summary[..LastErrorMaximumLength];
    }
}
