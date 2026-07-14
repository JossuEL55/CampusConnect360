using AttendanceService.Domain.Entities;

namespace AttendanceService.Infrastructure.Messaging;

public interface IOutboxMessagePublisher
{
    Task PublishAsync(
        OutboxMessage message,
        CancellationToken cancellationToken);
}
