using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Messaging;

public interface IOutboxMessagePublisher
{
    Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken);
}
