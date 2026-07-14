using Microsoft.EntityFrameworkCore;
using NotificationService.Infrastructure.Persistence;

namespace NotificationService.Infrastructure.Messaging;

public sealed class OutboxProcessor(
    NotificationDbContext dbContext,
    IOutboxMessagePublisher publisher,
    TimeProvider timeProvider,
    ILogger<OutboxProcessor> logger)
{
    public async Task<bool> PublishPendingBatchAsync(CancellationToken cancellationToken)
    {
        var messages = await dbContext.OutboxMessages
            .Where(x => x.ProcessedAt == null)
            .OrderBy(x => x.CreatedAt)
            .Take(20)
            .ToListAsync(cancellationToken);
        if (messages.Count == 0) return false;

        logger.LogInformation(
            "Notification outbox batch started. MessageCount={MessageCount} " +
            "ServiceName={ServiceName}",
            messages.Count,
            "NotificationService");
        var publishedAny = false;
        foreach (var message in messages)
        {
            try
            {
                await publisher.PublishAsync(message, cancellationToken);
                message.ProcessedAt = timeProvider.GetUtcNow();
                message.LastError = null;
                await dbContext.SaveChangesAsync(cancellationToken);
                publishedAny = true;
                logger.LogInformation(
                    "Notification outbox event marked processed. EventId={EventId} " +
                    "EventType={EventType} RoutingKey={RoutingKey} Attempts={Attempts} " +
                    "CorrelationId={CorrelationId} ServiceName={ServiceName}",
                    message.EventId,
                    message.EventType,
                    message.RoutingKey,
                    message.Attempts,
                    message.CorrelationId,
                    "NotificationService");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                message.Attempts++;
                var summary = $"{exception.GetType().Name}: {exception.Message}";
                message.LastError = summary.Length <= 2000 ? summary : summary[..2000];
                await dbContext.SaveChangesAsync(cancellationToken);
                logger.LogError(
                    exception,
                    "Notification outbox publication failed. EventId={EventId} " +
                    "Attempts={Attempts} CorrelationId={CorrelationId} " +
                    "ServiceName={ServiceName}",
                    message.EventId,
                    message.Attempts,
                    message.CorrelationId,
                    "NotificationService");
                break;
            }
        }
        return publishedAny;
    }
}
