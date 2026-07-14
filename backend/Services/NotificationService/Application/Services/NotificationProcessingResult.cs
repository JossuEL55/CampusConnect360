namespace NotificationService.Application.Services;

public enum NotificationProcessingOutcome
{
    Duplicate,
    StudentUpdated,
    NotificationCreated,
    Skipped
}

public sealed record NotificationProcessingResult(
    NotificationProcessingOutcome Outcome,
    Guid? NotificationId = null);
