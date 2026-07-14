namespace NotificationService.Infrastructure.Messaging;

public sealed class OutboxPublisherWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxPublisherWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Delay = TimeSpan.FromSeconds(2);
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<OutboxProcessor>();
                if (!await processor.PublishPendingBatchAsync(stoppingToken))
                    await Task.Delay(Delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Notification outbox cycle failed; retrying. ServiceName={ServiceName}",
                    "NotificationService");
                await Task.Delay(Delay, stoppingToken);
            }
        }
    }
}
