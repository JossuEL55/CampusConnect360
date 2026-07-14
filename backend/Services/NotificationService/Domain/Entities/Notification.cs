namespace NotificationService.Domain.Entities;

public sealed class Notification
{
    public Guid Id { get; set; }
    public string SourceEventId { get; set; } = string.Empty;
    public string SourceEventType { get; set; } = string.Empty;
    public Guid? StudentId { get; set; }
    public string Channel { get; set; } = "Email";
    public string Recipient { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public int Attempts { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
    public string? FailureReason { get; set; }
    public string SourcePayload { get; set; } = "{}";
    public LocalStudent? Student { get; set; }
    public ICollection<NotificationAttempt> NotificationAttempts { get; set; } = [];
    public ICollection<FailedMessage> FailedMessages { get; set; } = [];
}
