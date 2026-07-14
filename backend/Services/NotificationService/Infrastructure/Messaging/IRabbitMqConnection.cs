using RabbitMQ.Client;

namespace NotificationService.Infrastructure.Messaging;

public interface IRabbitMqConnection : IAsyncDisposable
{
    bool IsOpen { get; }
    Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default);
}
