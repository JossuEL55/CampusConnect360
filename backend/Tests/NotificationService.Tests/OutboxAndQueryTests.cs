using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationService.Application.Services;
using NotificationService.Domain.Entities;
using NotificationService.Infrastructure.Messaging;
using NotificationService.Infrastructure.Persistence;

namespace NotificationService.Tests;

public sealed class OutboxAndQueryTests
{
    [Fact]
    public async Task WorkerConfirmation_MarksProcessed_AndDoesNotRepublish()
    {
        await using var db = CreateDb();
        var message = Message();
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var publisher = new FakePublisher();
        var processor = Processor(db, publisher);

        Assert.True(await processor.PublishPendingBatchAsync(TestContext.Current.CancellationToken));
        Assert.False(await processor.PublishPendingBatchAsync(TestContext.Current.CancellationToken));
        Assert.NotNull(message.ProcessedAt);
        Assert.Equal(1, publisher.Calls);
    }

    [Fact]
    public async Task WorkerFailure_LeavesPendingAndRecordsError()
    {
        await using var db = CreateDb();
        var message = Message();
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.False(await Processor(db, new FakePublisher(true))
            .PublishPendingBatchAsync(TestContext.Current.CancellationToken));

        Assert.Null(message.ProcessedAt);
        Assert.Equal(1, message.Attempts);
        Assert.Contains("publish failed", message.LastError);
    }

    [Fact]
    public async Task GetMissingNotification_ReturnsNotFoundResult()
    {
        await using var db = CreateDb();
        var result = await new NotificationQueryService(db)
            .GetNotificationAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);
        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.StatusCode);
    }

    private static NotificationDbContext CreateDb() => new(
        new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static OutboxProcessor Processor(
        NotificationDbContext db,
        IOutboxMessagePublisher publisher) => new(
            db, publisher, TimeProvider.System, NullLogger<OutboxProcessor>.Instance);

    private static OutboxMessage Message() => new()
    {
        Id = Guid.NewGuid(), EventId = Guid.NewGuid().ToString("D"),
        EventType = "NotificationSent",
        RoutingKey = "notifications.notification.sent",
        CorrelationId = "test-correlation", Payload = "{}",
        OccurredAt = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow
    };

    private sealed class FakePublisher(bool fail = false) : IOutboxMessagePublisher
    {
        public int Calls { get; private set; }
        public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
        {
            Calls++;
            return fail
                ? Task.FromException(new InvalidOperationException("publish failed"))
                : Task.CompletedTask;
        }
    }
}
