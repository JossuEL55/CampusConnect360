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
    string? FailureReason,
    IReadOnlyList<NotificationAttemptResponse> NotificationAttempts);

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    long TotalCount);
