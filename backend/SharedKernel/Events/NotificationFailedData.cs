namespace SharedKernel.Events;

public sealed record NotificationFailedData(
    Guid NotificationId,
    string SourceEventId,
    string SourceEventType,
    string Channel,
    string Recipient,
    int Attempts,
    string FailureReason);
