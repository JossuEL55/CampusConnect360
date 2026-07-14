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
    public string Status { get; set; } = "Sent";
    public int Attempts { get; set; } = 1;
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public string? FailureReason { get; set; }
    public LocalStudent? Student { get; set; }
    public ICollection<NotificationAttempt> NotificationAttempts { get; set; } = [];
}
