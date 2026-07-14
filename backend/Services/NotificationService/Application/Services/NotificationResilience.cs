using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NotificationService.Domain.Entities;
using NotificationService.Infrastructure.Persistence;
using SharedKernel.Events;
using SharedKernel.Messaging;

namespace NotificationService.Application.Services;

public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";
    public bool FailureMode { get; init; }
}

public sealed class NotificationFailureMode
{
    private int _enabled;
    public NotificationFailureMode(bool enabled) => _enabled = enabled ? 1 : 0;
    public bool Enabled => Volatile.Read(ref _enabled) == 1;
    public void Set(bool enabled) => Interlocked.Exchange(ref _enabled, enabled ? 1 : 0);
}

public sealed class NotificationProcessingCoordinator : IDisposable
{
    // Serializa worker y retry HTTP en una sola réplica. Para escalado horizontal
    // se requiere una reclamación PostgreSQL con SKIP LOCKED o estado Processing.
    private readonly SemaphoreSlim _gate = new(1, 1);
    public async Task<IDisposable> EnterAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        return new Releaser(_gate);
    }

    public void Dispose() => _gate.Dispose();
    private sealed class Releaser(SemaphoreSlim gate) : IDisposable
    {
        public void Dispose() => gate.Release();
    }
}

public sealed class NotificationDeliveryProcessor(
    NotificationDbContext dbContext,
    NotificationFailureMode failureMode,
    TimeProvider timeProvider,
    ILogger<NotificationDeliveryProcessor> logger)
{
    public const string SimulatedFailure = "Simulated notification delivery failure";
    private static readonly TimeSpan SecondAttemptDelay = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ThirdAttemptDelay = TimeSpan.FromSeconds(45);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<int> ProcessDueBatchAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var ids = await dbContext.Notifications.AsNoTracking()
            .Where(x => x.Status == "Pending" && x.NextAttemptAt != null && x.NextAttemptAt <= now)
            .OrderBy(x => x.NextAttemptAt).Select(x => x.Id).Take(20)
            .ToListAsync(cancellationToken);
        foreach (var id in ids)
            await ProcessNotificationAsync(id, cancellationToken);
        return ids.Count;
    }

    public async Task<bool> ProcessNotificationAsync(Guid id, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var notification = await dbContext.Notifications
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (notification is null || notification.Status != "Pending" ||
            notification.NextAttemptAt is null || notification.NextAttemptAt > now)
            return false;

        IDbContextTransaction? transaction = null;
        try
        {
            if (dbContext.Database.IsRelational())
                transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            notification.Attempts++;
            notification.LastAttemptAt = now;
            var attempt = new NotificationAttempt
            {
                Id = Guid.NewGuid(), NotificationId = notification.Id,
                AttemptNumber = notification.Attempts, CreatedAt = now
            };
            dbContext.NotificationAttempts.Add(attempt);
            logger.LogInformation(
                "Notification delivery attempt started. NotificationId={NotificationId} Attempt={Attempt} CorrelationId={CorrelationId} ServiceName={ServiceName}",
                notification.Id, notification.Attempts, notification.CorrelationId, "NotificationService");

            if (failureMode.Enabled)
                Fail(notification, attempt, now);
            else
                await SucceedAsync(notification, attempt, now, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            if (transaction is not null) await transaction.DisposeAsync();
        }
    }

    private void Fail(Notification notification, NotificationAttempt attempt, DateTimeOffset now)
    {
        attempt.Status = "Failed";
        attempt.ErrorMessage = SimulatedFailure;
        notification.FailureReason = SimulatedFailure;
        if (notification.Attempts < 3)
        {
            var delay = notification.Attempts == 1 ? SecondAttemptDelay : ThirdAttemptDelay;
            notification.NextAttemptAt = now.Add(delay);
            logger.LogWarning(
                "Notification delivery attempt failed; next attempt scheduled. NotificationId={NotificationId} Attempt={Attempt} NextAttemptAt={NextAttemptAt} FailureReason={FailureReason} ServiceName={ServiceName}",
                notification.Id, notification.Attempts, notification.NextAttemptAt,
                SimulatedFailure, "NotificationService");
            return;
        }

        notification.Status = "Failed";
        notification.NextAttemptAt = null;
        var failed = new FailedMessage
        {
            Id = Guid.NewGuid(), NotificationId = notification.Id,
            SourceEventId = notification.SourceEventId,
            SourceEventType = notification.SourceEventType,
            CorrelationId = notification.CorrelationId,
            OriginalPayload = notification.SourcePayload,
            FailureReason = SimulatedFailure, Attempts = notification.Attempts,
            FailedAt = now, Status = "Failed"
        };
        dbContext.FailedMessages.Add(failed);
        AddOutbox(notification, EventTypes.NotificationFailed, RoutingKeys.NotificationFailed,
            new NotificationFailedData(notification.Id, notification.SourceEventId,
                notification.SourceEventType, notification.Channel, notification.Recipient,
                notification.Attempts, SimulatedFailure), now);
        logger.LogError(
            "Notification delivery attempts exhausted. NotificationId={NotificationId} Attempts={Attempts} FailedMessageId={FailedMessageId} CorrelationId={CorrelationId} ServiceName={ServiceName}",
            notification.Id, notification.Attempts, failed.Id,
            notification.CorrelationId, "NotificationService");
    }

    private async Task SucceedAsync(Notification notification, NotificationAttempt attempt,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        attempt.Status = "Sent";
        notification.Status = "Sent";
        notification.SentAt = now;
        notification.NextAttemptAt = null;
        notification.FailureReason = null;
        AddOutbox(notification, EventTypes.NotificationSent, RoutingKeys.NotificationSent,
            new NotificationSentData(notification.Id, notification.SourceEventId,
                notification.SourceEventType, notification.Channel, notification.Recipient,
                notification.Attempts), now);

        var failed = await dbContext.FailedMessages
            .Where(x => x.NotificationId == notification.Id && x.Status == "Retried")
            .OrderByDescending(x => x.FailedAt).FirstOrDefaultAsync(cancellationToken);
        if (failed is not null)
        {
            failed.Status = "Resolved";
            failed.ResolvedAt = now;
            logger.LogInformation(
                "Retried failed message resolved. NotificationId={NotificationId} FailedMessageId={FailedMessageId} ServiceName={ServiceName}",
                notification.Id, failed.Id, "NotificationService");
        }
        logger.LogInformation(
            "Simulated notification delivered successfully. NotificationId={NotificationId} Attempt={Attempt} CorrelationId={CorrelationId} ServiceName={ServiceName}",
            notification.Id, notification.Attempts, notification.CorrelationId, "NotificationService");
    }

    private void AddOutbox<T>(Notification notification, string eventType,
        string routingKey, T data, DateTimeOffset now)
    {
        var envelope = new EventEnvelope<T>
        {
            EventId = Guid.NewGuid(), EventType = eventType, Version = 1,
            OccurredAt = now, CorrelationId = notification.CorrelationId,
            Source = "NotificationService", EntityId = notification.Id, Data = data
        };
        dbContext.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(), EventId = envelope.EventId.ToString("D"),
            EventType = eventType, RoutingKey = routingKey,
            CorrelationId = notification.CorrelationId,
            Payload = JsonSerializer.Serialize(envelope, JsonOptions),
            OccurredAt = now, CreatedAt = now
        });
    }
}

public enum NotificationRetryOutcome { Accepted, NotFound, Conflict, FailureModeEnabled }
public sealed record NotificationRetryResult(NotificationRetryOutcome Outcome, DateTimeOffset? NextAttemptAt = null);

public sealed class NotificationRetryService(
    NotificationDbContext dbContext,
    NotificationFailureMode failureMode,
    NotificationProcessingCoordinator coordinator,
    TimeProvider timeProvider,
    ILogger<NotificationRetryService> logger)
{
    public async Task<NotificationRetryResult> RetryAsync(Guid id, CancellationToken cancellationToken)
    {
        using var lease = await coordinator.EnterAsync(cancellationToken);
        if (failureMode.Enabled)
            return new(NotificationRetryOutcome.FailureModeEnabled);
        var notification = await dbContext.Notifications.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (notification is null) return new(NotificationRetryOutcome.NotFound);
        if (notification.Status != "Failed") return new(NotificationRetryOutcome.Conflict);
        var failed = await dbContext.FailedMessages
            .Where(x => x.NotificationId == id && x.Status == "Failed")
            .OrderByDescending(x => x.FailedAt).FirstOrDefaultAsync(cancellationToken);
        if (failed is null) return new(NotificationRetryOutcome.Conflict);

        IDbContextTransaction? transaction = null;
        try
        {
            if (dbContext.Database.IsRelational())
                transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var now = timeProvider.GetUtcNow();
            notification.Status = "Pending"; notification.Attempts = 0;
            notification.FailureReason = null; notification.SentAt = null;
            notification.LastAttemptAt = null; notification.NextAttemptAt = now.AddSeconds(5);
            failed.Status = "Retried"; failed.RetriedAt = now;
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            logger.LogInformation(
                "Manual notification retry accepted. NotificationId={NotificationId} FailedMessageId={FailedMessageId} NextAttemptAt={NextAttemptAt} CorrelationId={CorrelationId} ServiceName={ServiceName}",
                id, failed.Id, notification.NextAttemptAt, notification.CorrelationId, "NotificationService");
            return new(NotificationRetryOutcome.Accepted, notification.NextAttemptAt);
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        finally { if (transaction is not null) await transaction.DisposeAsync(); }
    }
}
