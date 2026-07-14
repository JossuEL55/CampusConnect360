namespace NotificationService.Application.Contracts;

public sealed class NotificationQueryParameters
{
    public string? Status { get; init; }
    public string? SourceEventType { get; init; }
    public Guid? StudentId { get; init; }
    public int? Page { get; init; }
    public int? PageSize { get; init; }
}

public sealed record NotificationListItemResponse(
    Guid Id,
    string SourceEventId,
    string SourceEventType,
    Guid? StudentId,
    string Channel,
    string Recipient,
    string Subject,
    string Status,
    int Attempts,
    string CorrelationId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SentAt);

public sealed record NotificationAttemptResponse(
    Guid Id,
    int AttemptNumber,
    string Status,
    string? ErrorMessage,
    DateTimeOffset CreatedAt);

public sealed record NotificationDetailResponse(
    Guid Id,
    string SourceEventId,
    string SourceEventType,
    Guid? StudentId,
    string Channel,
    string Recipient,
    string Subject,
    string Body,
    string Status,
    int Attempts,
    string CorrelationId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SentAt,
    DateTimeOffset? NextAttemptAt,
    DateTimeOffset? LastAttemptAt,
    string? FailureReason,
    IReadOnlyList<NotificationAttemptResponse> NotificationAttempts,
    FailedMessageResponse? Failure);

public sealed class FailedMessageQueryParameters
{
    public string? Status { get; init; }
    public string? SourceEventType { get; init; }
    public int? Page { get; init; }
    public int? PageSize { get; init; }
}

public sealed record FailedMessageResponse(
    Guid FailedMessageId,
    Guid NotificationId,
    string SourceEventId,
    string SourceEventType,
    string CorrelationId,
    int Attempts,
    string FailureReason,
    DateTimeOffset FailedAt,
    string Status,
    DateTimeOffset? RetriedAt,
    DateTimeOffset? ResolvedAt);

public sealed record FailureModeRequest(bool Enabled);
public sealed record FailureModeResponse(bool Enabled, DateTimeOffset UpdatedAt);
public sealed record NotificationRetryResponse(Guid NotificationId, string Status, DateTimeOffset NextAttemptAt);

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    long TotalCount);
