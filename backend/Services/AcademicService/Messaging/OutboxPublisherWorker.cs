using AcademicService.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AcademicService.Messaging;

public sealed class OutboxPublisherWorker(
    IServiceScopeFactory scopeFactory,
    IIntegrationEventPublisher publisher,
    ILogger<OutboxPublisherWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var publishedAny = await PublishPendingBatchAsync(stoppingToken);
            if (!publishedAny)
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }

    private async Task<bool> PublishPendingBatchAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AcademicDbContext>();
        var pending = await db.OutboxMessages
            .Where(x => x.DispatchedAt == null)
            .OrderBy(x => x.OccurredAt)
            .Take(20)
            .ToListAsync(ct);
        var publishedAny = false;

        foreach (var message in pending)
        {
            try
            {
                await publisher.PublishAsync(message, ct);
                message.DispatchedAt = DateTimeOffset.UtcNow;
                message.LastError = null;
                publishedAny = true;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                message.Attempts++;
                message.LastError = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
                logger.LogError(
                    ex,
                    "No se pudo publicar el evento outbox {EventId}; intento {Attempt}.",
                    message.EventId,
                    message.Attempts);
            }

            await db.SaveChangesAsync(ct);
            if (message.DispatchedAt is null) break;
        }

        return publishedAny;
    }
}
