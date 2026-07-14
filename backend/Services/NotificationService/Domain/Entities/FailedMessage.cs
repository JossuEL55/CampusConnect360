namespace NotificationService.Domain.Entities;

public sealed class FailedMessage
{
    public Guid Id { get; set; }
    public Guid NotificationId { get; set; }
    public string SourceEventId { get; set; } = string.Empty;
    public string SourceEventType { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string OriginalPayload { get; set; } = "{}";
    public string FailureReason { get; set; } = string.Empty;
    public int Attempts { get; set; }
    public DateTimeOffset FailedAt { get; set; }
    public DateTimeOffset? RetriedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string Status { get; set; } = "Failed";
    public DateTimeOffset? DeadLetterPublishedAt { get; set; }
    public int DeadLetterAttempts { get; set; }
    public string? DeadLetterLastError { get; set; }
    public Notification Notification { get; set; } = null!;
}
