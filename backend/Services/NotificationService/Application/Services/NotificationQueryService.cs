using Microsoft.EntityFrameworkCore;
using NotificationService.Application.Contracts;
using NotificationService.Infrastructure.Persistence;

namespace NotificationService.Application.Services;

public sealed record NotificationQueryResult<T>(
    T? Value,
    int StatusCode,
    IReadOnlyDictionary<string, string[]>? Errors = null)
{
    public bool IsSuccess => StatusCode is >= 200 and < 300;
}

public sealed class NotificationQueryService(NotificationDbContext dbContext)
{
    private static readonly HashSet<string> SourceEventTypes =
    [
        SharedKernel.Events.EventTypes.StudentEnrolled,
        SharedKernel.Events.EventTypes.PaymentConfirmed,
        SharedKernel.Events.EventTypes.AttendanceRecorded,
        SharedKernel.Events.EventTypes.IncidentReported
    ];

    public async Task<NotificationQueryResult<PagedResponse<NotificationListItemResponse>>>
        GetNotificationsAsync(
            NotificationQueryParameters parameters,
            CancellationToken cancellationToken)
    {
        var page = parameters.Page ?? 1;
        var pageSize = parameters.PageSize ?? 20;
        var errors = Validate(parameters, page, pageSize);
        if (errors.Count > 0)
        {
            return new(null, StatusCodes.Status400BadRequest, errors);
        }

        var query = dbContext.Notifications.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(parameters.Status))
            query = query.Where(x => x.Status == parameters.Status);
        if (!string.IsNullOrWhiteSpace(parameters.SourceEventType))
            query = query.Where(x => x.SourceEventType == parameters.SourceEventType);
        if (parameters.StudentId.HasValue)
            query = query.Where(x => x.StudentId == parameters.StudentId.Value);

        var total = await query.LongCountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new NotificationListItemResponse(
                x.Id, x.SourceEventId, x.SourceEventType, x.StudentId,
                x.Channel, x.Recipient, x.Subject, x.Status, x.Attempts,
                x.CorrelationId, x.CreatedAt, x.SentAt))
            .ToListAsync(cancellationToken);
        return new(new(items, page, pageSize, total), StatusCodes.Status200OK);
    }

    public async Task<NotificationQueryResult<NotificationDetailResponse>>
        GetNotificationAsync(Guid id, CancellationToken cancellationToken)
    {
        var notification = await dbContext.Notifications.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new NotificationDetailResponse(
                x.Id, x.SourceEventId, x.SourceEventType, x.StudentId,
                x.Channel, x.Recipient, x.Subject, x.Body, x.Status,
                x.Attempts, x.CorrelationId, x.CreatedAt, x.SentAt,
                x.NextAttemptAt, x.LastAttemptAt, x.FailureReason,
                x.NotificationAttempts.OrderBy(a => a.AttemptNumber)
                    .Select(a => new NotificationAttemptResponse(
                        a.Id, a.AttemptNumber, a.Status, a.ErrorMessage,
                        a.CreatedAt)).ToList(),
                x.FailedMessages.OrderByDescending(f => f.FailedAt)
                    .Select(f => new FailedMessageResponse(
                        f.Id, f.NotificationId, f.SourceEventId, f.SourceEventType,
                        f.CorrelationId, f.Attempts, f.FailureReason, f.FailedAt,
                        f.Status, f.RetriedAt, f.ResolvedAt)).FirstOrDefault()))
            .SingleOrDefaultAsync(cancellationToken);
        return notification is null
            ? new(null, StatusCodes.Status404NotFound)
            : new(notification, StatusCodes.Status200OK);
    }

    public async Task<NotificationQueryResult<PagedResponse<FailedMessageResponse>>>
        GetFailedMessagesAsync(FailedMessageQueryParameters parameters,
            CancellationToken cancellationToken)
    {
        var page = parameters.Page ?? 1;
        var pageSize = parameters.PageSize ?? 20;
        var errors = new Dictionary<string, string[]>();
        if (page < 1) errors["page"] = ["Page debe ser mayor o igual a 1."];
        if (pageSize is < 1 or > 100) errors["pageSize"] = ["PageSize debe estar entre 1 y 100."];
        if (parameters.Status is not null && parameters.Status is not ("Failed" or "Retried" or "Resolved"))
            errors["status"] = ["Status debe ser Failed, Retried o Resolved."];
        if (parameters.SourceEventType is not null && !SourceEventTypes.Contains(parameters.SourceEventType))
            errors["sourceEventType"] = ["SourceEventType no es válido."];
        if (errors.Count > 0) return new(null, StatusCodes.Status400BadRequest, errors);

        var query = dbContext.FailedMessages.AsNoTracking();
        if (parameters.Status is not null) query = query.Where(x => x.Status == parameters.Status);
        if (parameters.SourceEventType is not null)
            query = query.Where(x => x.SourceEventType == parameters.SourceEventType);
        var total = await query.LongCountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.FailedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new FailedMessageResponse(x.Id, x.NotificationId,
                x.SourceEventId, x.SourceEventType, x.CorrelationId, x.Attempts,
                x.FailureReason, x.FailedAt, x.Status, x.RetriedAt, x.ResolvedAt))
            .ToListAsync(cancellationToken);
        return new(new(items, page, pageSize, total), StatusCodes.Status200OK);
    }

    private static Dictionary<string, string[]> Validate(
        NotificationQueryParameters parameters,
        int page,
        int pageSize)
    {
        var errors = new Dictionary<string, string[]>();
        if (page < 1) errors["page"] = ["Page debe ser mayor o igual a 1."];
        if (pageSize is < 1 or > 100)
            errors["pageSize"] = ["PageSize debe estar entre 1 y 100."];
        if (parameters.Status is not null && parameters.Status is not ("Pending" or "Sent" or "Failed"))
            errors["status"] = ["Status debe ser Pending, Sent o Failed."];
        if (parameters.SourceEventType is not null &&
            !SourceEventTypes.Contains(parameters.SourceEventType))
            errors["sourceEventType"] = ["SourceEventType no es válido."];
        if (parameters.StudentId == Guid.Empty)
            errors["studentId"] = ["StudentId debe ser un UUID válido."];
        return errors;
    }
}
