using AttendanceService.Domain.Entities;
using AttendanceService.Infrastructure.Messaging;
using AttendanceService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel.Events;
using SharedKernel.Messaging;

namespace AttendanceService.Tests;

public sealed class OutboxProcessorTests
{
    [Fact]
    public async Task ConfirmedPublication_MarksProcessed_AndDoesNotRepublish()
    {
        await using var dbContext = CreateDbContext();
        var message = CreateMessage();
        dbContext.OutboxMessages.Add(message);
        await dbContext.SaveChangesAsync(
            TestContext.Current.CancellationToken);
        var publisher = new FakePublisher();
        var processor = CreateProcessor(dbContext, publisher);

        var published = await processor.PublishPendingBatchAsync(
            TestContext.Current.CancellationToken);
        var publishedAgain = await processor.PublishPendingBatchAsync(
            TestContext.Current.CancellationToken);

        Assert.True(published);
        Assert.False(publishedAgain);
        Assert.Equal(1, publisher.CallCount);
        Assert.False(publisher.WasProcessedWhenPublished);
        Assert.NotNull(message.ProcessedAt);
        Assert.Null(message.LastError);
        Assert.Equal(0, message.Attempts);
    }

    [Fact]
    public async Task FailedPublication_LeavesPendingAndRecordsAttemptAndError()
    {
        await using var dbContext = CreateDbContext();
        var message = CreateMessage();
        dbContext.OutboxMessages.Add(message);
        await dbContext.SaveChangesAsync(
            TestContext.Current.CancellationToken);
        var publisher = new FakePublisher(fail: true);
        var processor = CreateProcessor(dbContext, publisher);

        var published = await processor.PublishPendingBatchAsync(
            TestContext.Current.CancellationToken);

        Assert.False(published);
        Assert.Equal(1, publisher.CallCount);
        Assert.Null(message.ProcessedAt);
        Assert.Equal(1, message.Attempts);
        Assert.Contains("Simulated publish failure", message.LastError);
    }

    private static OutboxProcessor CreateProcessor(
        AttendanceDbContext dbContext,
        IOutboxMessagePublisher publisher) => new(
            dbContext,
            publisher,
            TimeProvider.System,
            NullLogger<OutboxProcessor>.Instance);

    private static AttendanceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AttendanceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AttendanceDbContext(options);
    }

    private static OutboxMessage CreateMessage()
    {
        var now = DateTimeOffset.UtcNow;
        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid().ToString("D"),
            EventType = EventTypes.AttendanceRecorded,
            RoutingKey = RoutingKeys.AttendanceRecorded,
            CorrelationId = "outbox-processor-correlation",
            Payload = "{}",
            OccurredAt = now,
            CreatedAt = now
        };
    }

    private sealed class FakePublisher(bool fail = false) :
        IOutboxMessagePublisher
    {
        public int CallCount { get; private set; }
        public bool WasProcessedWhenPublished { get; private set; }

        public Task PublishAsync(
            OutboxMessage message,
            CancellationToken cancellationToken)
        {
            CallCount++;
            WasProcessedWhenPublished = message.ProcessedAt.HasValue;

            return fail
                ? Task.FromException(
                    new InvalidOperationException(
                        "Simulated publish failure."))
                : Task.CompletedTask;
        }
    }
}
