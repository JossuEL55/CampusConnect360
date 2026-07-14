using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationService.Application.Contracts;
using NotificationService.Application.Services;
using NotificationService.Domain.Entities;
using NotificationService.Infrastructure.Messaging;
using NotificationService.Infrastructure.Persistence;
using RabbitMQ.Client;
using SharedKernel.Events;
using SharedKernel.Messaging;

namespace NotificationService.Tests;

public sealed class NotificationResilienceTests
{
    [Fact]
    public async Task FirstFailure_RecordsAttempt_AndSchedulesSecondAfter15Seconds()
    {
        await using var db = CreateDb();
        var clock = Clock();
        var notification = AddPending(db, clock.GetUtcNow());
        var processor = Delivery(db, clock, enabled: true);

        Assert.True(await processor.ProcessNotificationAsync(notification.Id, Ct));

        Assert.Equal(1, notification.Attempts);
        Assert.Equal("Pending", notification.Status);
        Assert.Equal(clock.GetUtcNow().AddSeconds(15), notification.NextAttemptAt);
        var attempt = Assert.Single(db.NotificationAttempts);
        Assert.Equal("Failed", attempt.Status);
        Assert.Equal(NotificationDeliveryProcessor.SimulatedFailure, attempt.ErrorMessage);
    }

    [Fact]
    public async Task SecondFailure_SchedulesThirdAfter45Seconds()
    {
        await using var db = CreateDb();
        var clock = Clock();
        var notification = AddPending(db, clock.GetUtcNow(), attempts: 1);

        await Delivery(db, clock, true).ProcessNotificationAsync(notification.Id, Ct);

        Assert.Equal(2, notification.Attempts);
        Assert.Equal(clock.GetUtcNow().AddSeconds(45), notification.NextAttemptAt);
    }

    [Fact]
    public async Task ThirdFailure_MarksFailed_AndCreatesFailedMessageAndNotificationFailedOutbox()
    {
        await using var db = CreateDb();
        var clock = Clock();
        var notification = AddPending(db, clock.GetUtcNow(), attempts: 2);

        await Delivery(db, clock, true).ProcessNotificationAsync(notification.Id, Ct);

        Assert.Equal("Failed", notification.Status);
        Assert.Equal(3, notification.Attempts);
        Assert.Null(notification.NextAttemptAt);
        var failed = Assert.Single(db.FailedMessages);
        Assert.Equal(notification.SourcePayload, failed.OriginalPayload);
        Assert.Equal("Failed", failed.Status);
        var outbox = Assert.Single(db.OutboxMessages);
        Assert.Equal(EventTypes.NotificationFailed, outbox.EventType);
        Assert.Equal(RoutingKeys.NotificationFailed, outbox.RoutingKey);
        using var json = JsonDocument.Parse(outbox.Payload);
        var root = json.RootElement;
        Assert.Equal("NotificationService", root.GetProperty("source").GetString());
        Assert.Equal(notification.Id, root.GetProperty("entityId").GetGuid());
        var data = root.GetProperty("data");
        Assert.Equal(3, data.GetProperty("attempts").GetInt32());
        Assert.Equal(NotificationDeliveryProcessor.SimulatedFailure,
            data.GetProperty("failureReason").GetString());
    }

    [Fact]
    public async Task NormalDelivery_MarksSent_AndCreatesNotificationSentOutbox()
    {
        await using var db = CreateDb();
        var clock = Clock();
        var notification = AddPending(db, clock.GetUtcNow());

        await Delivery(db, clock, false).ProcessNotificationAsync(notification.Id, Ct);

        Assert.Equal("Sent", notification.Status);
        Assert.Equal(1, notification.Attempts);
        Assert.Equal(clock.GetUtcNow(), notification.SentAt);
        Assert.Null(notification.FailureReason);
        Assert.Equal("Sent", Assert.Single(db.NotificationAttempts).Status);
        Assert.Equal(EventTypes.NotificationSent, Assert.Single(db.OutboxMessages).EventType);
    }

    [Fact]
    public void FailureMode_CanBeEnabledAndDisabled()
    {
        var mode = new NotificationFailureMode(false);
        mode.Set(true); Assert.True(mode.Enabled);
        mode.Set(false); Assert.False(mode.Enabled);
    }

    [Fact]
    public async Task RetryFailed_ResetsSameNotificationAndMarksFailedMessageRetried()
    {
        await using var db = CreateDb();
        var clock = Clock();
        var notification = AddFailed(db, clock.GetUtcNow());
        using var coordinator = new NotificationProcessingCoordinator();
        var service = new NotificationRetryService(db, new(false), coordinator, clock,
            NullLogger<NotificationRetryService>.Instance);

        var result = await service.RetryAsync(notification.Id, Ct);

        Assert.Equal(NotificationRetryOutcome.Accepted, result.Outcome);
        Assert.Equal("Pending", notification.Status);
        Assert.Equal(0, notification.Attempts);
        Assert.Equal(clock.GetUtcNow().AddSeconds(5), notification.NextAttemptAt);
        Assert.Single(db.Notifications);
        Assert.Equal("Retried", Assert.Single(db.FailedMessages).Status);
    }

    [Theory]
    [InlineData("Sent")]
    [InlineData("Pending")]
    public async Task RetryNonFailed_ReturnsConflict(string status)
    {
        await using var db = CreateDb();
        var clock = Clock();
        var notification = AddPending(db, clock.GetUtcNow());
        notification.Status = status;
        await db.SaveChangesAsync(Ct);
        using var coordinator = new NotificationProcessingCoordinator();
        var service = new NotificationRetryService(db, new(false), coordinator, clock,
            NullLogger<NotificationRetryService>.Instance);

        Assert.Equal(NotificationRetryOutcome.Conflict,
            (await service.RetryAsync(notification.Id, Ct)).Outcome);
    }

    [Fact]
    public async Task RetryWhileFailureModeEnabled_ReturnsConflictOutcome()
    {
        await using var db = CreateDb();
        var clock = Clock();
        var notification = AddFailed(db, clock.GetUtcNow());
        using var coordinator = new NotificationProcessingCoordinator();
        var service = new NotificationRetryService(db, new(true), coordinator, clock,
            NullLogger<NotificationRetryService>.Instance);

        Assert.Equal(NotificationRetryOutcome.FailureModeEnabled,
            (await service.RetryAsync(notification.Id, Ct)).Outcome);
    }

    [Fact]
    public async Task SuccessfulDeliveryAfterRetry_ResolvesFailedMessage()
    {
        await using var db = CreateDb();
        var clock = Clock();
        var notification = AddFailed(db, clock.GetUtcNow());
        notification.Status = "Pending"; notification.Attempts = 0;
        notification.NextAttemptAt = clock.GetUtcNow();
        var failed = Assert.Single(db.FailedMessages); failed.Status = "Retried";
        await db.SaveChangesAsync(Ct);

        await Delivery(db, clock, false).ProcessNotificationAsync(notification.Id, Ct);

        Assert.Equal("Resolved", failed.Status);
        Assert.Equal(clock.GetUtcNow(), failed.ResolvedAt);
    }

    [Fact]
    public async Task WorkerDoesNotProcessBeforeDue_OrProcessSentTwice()
    {
        await using var db = CreateDb();
        var clock = Clock();
        var notification = AddPending(db, clock.GetUtcNow().AddSeconds(5));
        var processor = Delivery(db, clock, false);

        Assert.Equal(0, await processor.ProcessDueBatchAsync(Ct));
        clock.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(1, await processor.ProcessDueBatchAsync(Ct));
        Assert.Equal(0, await processor.ProcessDueBatchAsync(Ct));
        Assert.Single(db.NotificationAttempts);
    }

    [Fact]
    public async Task DlqQuery_PaginatesWithoutExposingPayload_AndDetailIncludesAttemptsAndFailure()
    {
        await using var db = CreateDb();
        var clock = Clock();
        var notification = AddFailed(db, clock.GetUtcNow());
        db.NotificationAttempts.Add(new NotificationAttempt
        {
            Id = Guid.NewGuid(), NotificationId = notification.Id,
            AttemptNumber = 3, Status = "Failed", CreatedAt = clock.GetUtcNow()
        });
        await db.SaveChangesAsync(Ct);
        var query = new NotificationQueryService(db);

        var page = await query.GetFailedMessagesAsync(new() { Page = 1, PageSize = 1 }, Ct);
        var detail = await query.GetNotificationAsync(notification.Id, Ct);

        Assert.True(page.IsSuccess);
        Assert.Single(page.Value!.Items);
        Assert.Equal(1, page.Value.TotalCount);
        Assert.Single(detail.Value!.NotificationAttempts);
        Assert.NotNull(detail.Value.Failure);
    }

    [Fact]
    public async Task DeadLetterProcessor_PublishesOnceAfterConfirmation()
    {
        await using var db = CreateDb();
        var clock = Clock();
        AddFailed(db, clock.GetUtcNow());
        var publisher = new FakeDeadLetterPublisher();
        var processor = new DeadLetterProcessor(db, publisher, clock,
            NullLogger<DeadLetterProcessor>.Instance);

        Assert.True(await processor.PublishPendingBatchAsync(Ct));
        Assert.False(await processor.PublishPendingBatchAsync(Ct));
        Assert.Equal(1, publisher.Calls);
        Assert.NotNull(Assert.Single(db.FailedMessages).DeadLetterPublishedAt);
    }

    [Fact]
    public void DeadLetterProperties_ArePersistentAndContainRequiredHeaders()
    {
        var failed = Failed(Guid.NewGuid(), DateTimeOffset.UtcNow);
        var properties = RabbitMqDeadLetterPublisher.CreateProperties(failed);

        Assert.Equal(DeliveryModes.Persistent, properties.DeliveryMode);
        Assert.Equal(failed.Id.ToString("D"), properties.MessageId);
        Assert.Equal(failed.CorrelationId, properties.CorrelationId);
        Assert.Equal("NotificationDeliveryFailed", properties.Type);
        Assert.Equal("NotificationService", properties.AppId);
        Assert.Equal(failed.Attempts, properties.Headers!["x-retry-count"]);
        Assert.Equal(failed.SourceEventId, properties.Headers["x-original-event-id"]);
        Assert.Equal(failed.NotificationId.ToString("D"), properties.Headers["x-notification-id"]);
        Assert.Equal("notifications.dead-letter", RabbitMqDeadLetterPublisher.RoutingKey);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static NotificationDbContext CreateDb() => new(
        new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static MutableTimeProvider Clock() => new(new DateTimeOffset(2026, 7, 14, 2, 0, 0, TimeSpan.Zero));
    private static NotificationDeliveryProcessor Delivery(NotificationDbContext db,
        MutableTimeProvider clock, bool enabled) => new(db, new(enabled), clock,
            NullLogger<NotificationDeliveryProcessor>.Instance);

    private static Notification AddPending(NotificationDbContext db, DateTimeOffset due, int attempts = 0)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(), SourceEventId = Guid.NewGuid().ToString("D"),
            SourceEventType = EventTypes.AttendanceRecorded, Channel = "Email",
            Recipient = "guardian@example.test", Subject = "Ausencia", Body = "Body",
            Status = "Pending", Attempts = attempts, CorrelationId = "test-correlation",
            CreatedAt = due.AddSeconds(-5), NextAttemptAt = due,
            SourcePayload = """{"eventType":"AttendanceRecorded"}"""
        };
        db.Notifications.Add(notification); db.SaveChanges(); return notification;
    }

    private static Notification AddFailed(NotificationDbContext db, DateTimeOffset now)
    {
        var notification = AddPending(db, now, 3);
        notification.Status = "Failed"; notification.NextAttemptAt = null;
        notification.FailureReason = NotificationDeliveryProcessor.SimulatedFailure;
        db.FailedMessages.Add(Failed(notification.Id, now));
        db.SaveChanges(); return notification;
    }

    private static FailedMessage Failed(Guid notificationId, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), NotificationId = notificationId,
        SourceEventId = Guid.NewGuid().ToString("D"),
        SourceEventType = EventTypes.AttendanceRecorded,
        CorrelationId = "test-correlation", OriginalPayload = "{}",
        FailureReason = NotificationDeliveryProcessor.SimulatedFailure,
        Attempts = 3, FailedAt = now, Status = "Failed"
    };

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan value) => _now = _now.Add(value);
    }

    private sealed class FakeDeadLetterPublisher : IDeadLetterPublisher
    {
        public int Calls { get; private set; }
        public Task PublishAsync(FailedMessage message, CancellationToken cancellationToken)
        { Calls++; return Task.CompletedTask; }
    }
}
