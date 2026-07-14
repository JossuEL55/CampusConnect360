using System.ComponentModel.DataAnnotations;
using PaymentService.Domain;

namespace PaymentService.Api;

public sealed record DebtRequest(
    Guid StudentId,
    [property: Required(ErrorMessage = "concept es obligatorio."),
               MaxLength(120, ErrorMessage = "concept no puede superar 120 caracteres.")]
    string Concept,
    decimal Amount,
    DateOnly DueDate);

public sealed record ConfirmPaymentRequest(
    [property: Required(ErrorMessage = "paymentMethod es obligatorio."),
               MaxLength(60, ErrorMessage = "paymentMethod no puede superar 60 caracteres.")]
    string PaymentMethod,
    [property: Required(ErrorMessage = "reference es obligatorio."),
               MaxLength(80, ErrorMessage = "reference no puede superar 80 caracteres.")]
    string Reference,
    decimal PaidAmount);

public sealed record StudentSummaryResponse(Guid StudentId, string StudentCode, string FullName,
    string Grade, string SchoolId, string SchoolYear, string GuardianEmail, decimal PendingAmount,
    int PendingDebts, DateTimeOffset EnrolledAt);

public sealed record DebtResponse(Guid DebtId, Guid StudentId, string StudentFullName, string Concept,
    decimal Amount, DateOnly DueDate, DebtStatus Status, DateTimeOffset CreatedAt, DateTimeOffset? PaidAt);

public sealed record PaymentResponse(Guid PaymentId, Guid DebtId, Guid StudentId, decimal Amount,
    PaymentStatus Status, string PaymentMethod, string Reference, DateTimeOffset ConfirmedAt);

public sealed record StudentPaymentHistoryResponse(Guid StudentId, string StudentCode, string FullName,
    IReadOnlyList<DebtResponse> Debts, IReadOnlyList<PaymentResponse> Payments);

public sealed record PaymentEventResponse(Guid EventId, string EventType, string CorrelationId,
    string Payload, DateTimeOffset OccurredAt);

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, long TotalCount);

public sealed record StudentEnrolledData(Guid StudentId, string StudentCode, string FullName, string Grade,
    string SchoolId, string SchoolYear, string GuardianEmail, Guid EnrollmentId);

public sealed record PaymentConfirmedData(Guid PaymentId, Guid DebtId, Guid StudentId, string Concept,
    decimal Amount, string PaymentMethod, DateTimeOffset ConfirmedAt);
