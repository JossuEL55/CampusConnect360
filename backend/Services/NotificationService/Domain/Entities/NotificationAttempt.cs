namespace NotificationService.Domain.Entities;

public sealed class NotificationAttempt
{
    public Guid Id { get; set; }
    public Guid NotificationId { get; set; }
    public int AttemptNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Notification Notification { get; set; } = null!;
}
