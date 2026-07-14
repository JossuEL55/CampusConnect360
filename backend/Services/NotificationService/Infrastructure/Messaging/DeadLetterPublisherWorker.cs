namespace NotificationService.Infrastructure.Messaging;

public sealed class DeadLetterPublisherWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<DeadLetterPublisherWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Delay = TimeSpan.FromSeconds(2);
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<DeadLetterProcessor>();
                if (!await processor.PublishPendingBatchAsync(stoppingToken))
                    await Task.Delay(Delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                logger.LogError(exception,
                    "DLQ publisher cycle failed; retrying. ServiceName={ServiceName}",
                    "NotificationService");
                await Task.Delay(Delay, stoppingToken);
            }
        }
    }
}
