namespace SharedKernel.Events;

public sealed record NotificationSentData(
    Guid NotificationId,
    string SourceEventId,
    string SourceEventType,
    string Channel,
    string Recipient,
    int Attempts);
