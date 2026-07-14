using Microsoft.EntityFrameworkCore;
using NotificationService.Infrastructure.Persistence;

namespace NotificationService.Infrastructure.Messaging;

public sealed class DeadLetterProcessor(
    NotificationDbContext dbContext,
    IDeadLetterPublisher publisher,
    TimeProvider timeProvider,
    ILogger<DeadLetterProcessor> logger)
{
    public async Task<bool> PublishPendingBatchAsync(CancellationToken cancellationToken)
    {
        var messages = await dbContext.FailedMessages
            .Where(x => x.DeadLetterPublishedAt == null)
            .OrderBy(x => x.FailedAt).Take(20).ToListAsync(cancellationToken);
        if (messages.Count == 0) return false;
        foreach (var message in messages)
        {
            try
            {
                await publisher.PublishAsync(message, cancellationToken);
                message.DeadLetterPublishedAt = timeProvider.GetUtcNow();
                message.DeadLetterLastError = null;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                message.DeadLetterAttempts++;
                var error = $"{exception.GetType().Name}: {exception.Message}";
                message.DeadLetterLastError = error.Length <= 2000 ? error : error[..2000];
                await dbContext.SaveChangesAsync(cancellationToken);
                logger.LogError(exception,
                    "DLQ publication failed. FailedMessageId={FailedMessageId} Attempts={Attempts} ServiceName={ServiceName}",
                    message.Id, message.DeadLetterAttempts, "NotificationService");
                break;
            }
        }
        return true;
    }
}
