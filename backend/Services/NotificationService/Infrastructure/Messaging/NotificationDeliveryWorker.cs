using NotificationService.Application.Services;

namespace NotificationService.Infrastructure.Messaging;

public sealed class NotificationDeliveryWorker(
    IServiceScopeFactory scopeFactory,
    NotificationProcessingCoordinator coordinator,
    ILogger<NotificationDeliveryWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var lease = await coordinator.EnterAsync(stoppingToken);
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<NotificationDeliveryProcessor>();
                await processor.ProcessDueBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                logger.LogError(exception,
                    "Notification delivery cycle failed; retrying. ServiceName={ServiceName}",
                    "NotificationService");
            }
            await Task.Delay(PollInterval, stoppingToken);
        }
    }
}
