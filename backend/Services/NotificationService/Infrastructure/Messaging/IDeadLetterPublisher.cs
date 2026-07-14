using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Messaging;

public interface IDeadLetterPublisher
{
    Task PublishAsync(FailedMessage message, CancellationToken cancellationToken);
}
