namespace AttendanceService.Infrastructure.Messaging;

public sealed class OutboxPublisherWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxPublisherWorker> logger) : BackgroundService
{
    private static readonly TimeSpan CycleDelay = TimeSpan.FromSeconds(2);
    private readonly SemaphoreSlim _cycleLock = new(1, 1);

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var publishedAny = await RunCycleAsync(stoppingToken);
                if (!publishedAny)
                {
                    await Task.Delay(CycleDelay, stoppingToken);
                }
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Attendance outbox cycle failed; retrying in " +
                    "{DelaySeconds} seconds. ServiceName={ServiceName}",
                    CycleDelay.TotalSeconds,
                    "AttendanceService");
                await Task.Delay(CycleDelay, stoppingToken);
            }
        }
    }

    private async Task<bool> RunCycleAsync(
        CancellationToken cancellationToken)
    {
        await _cycleLock.WaitAsync(cancellationToken);
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var processor = scope.ServiceProvider
                .GetRequiredService<OutboxProcessor>();
            return await processor.PublishPendingBatchAsync(
                cancellationToken);
        }
        finally
        {
            _cycleLock.Release();
        }
    }

    public override void Dispose()
    {
        _cycleLock.Dispose();
        base.Dispose();
    }
}
