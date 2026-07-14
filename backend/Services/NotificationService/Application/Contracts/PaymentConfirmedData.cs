namespace NotificationService.Application.Contracts;

public sealed record PaymentConfirmedData(
    Guid PaymentId,
    Guid DebtId,
    Guid StudentId,
    string Concept,
    decimal Amount,
    string PaymentMethod,
    DateTimeOffset ConfirmedAt);
