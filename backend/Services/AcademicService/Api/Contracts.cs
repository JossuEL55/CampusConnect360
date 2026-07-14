using System.ComponentModel.DataAnnotations;
using AcademicService.Domain;

namespace AcademicService.Api;

public sealed record GuardianRequest(
    [property: Required(ErrorMessage = "guardian.fullName es obligatorio."),
               MaxLength(120, ErrorMessage = "guardian.fullName no puede superar 120 caracteres.")]
    string FullName,
    [property: Required(ErrorMessage = "guardian.email es obligatorio."),
               EmailAddress(ErrorMessage = "guardian.email debe tener un formato válido."),
               MaxLength(160, ErrorMessage = "guardian.email no puede superar 160 caracteres.")]
    string Email,
    [property: Required(ErrorMessage = "guardian.phone es obligatorio."),
               MaxLength(30, ErrorMessage = "guardian.phone no puede superar 30 caracteres.")]
    string Phone);

public sealed record StudentRequest(
    [property: Required(ErrorMessage = "identification es obligatorio."),
               MaxLength(20, ErrorMessage = "identification no puede superar 20 caracteres.")]
    string Identification,
    [property: Required(ErrorMessage = "firstName es obligatorio."),
               MaxLength(80, ErrorMessage = "firstName no puede superar 80 caracteres.")]
    string FirstName,
    [property: Required(ErrorMessage = "lastName es obligatorio."),
               MaxLength(80, ErrorMessage = "lastName no puede superar 80 caracteres.")]
    string LastName,
    DateOnly BirthDate,
    [property: Required(ErrorMessage = "grade es obligatorio."),
               MaxLength(40, ErrorMessage = "grade no puede superar 40 caracteres.")]
    string Grade,
    [property: Required(ErrorMessage = "schoolId es obligatorio."),
               MaxLength(30, ErrorMessage = "schoolId no puede superar 30 caracteres.")]
    string SchoolId,
    [property: Required(ErrorMessage = "guardian es obligatorio.")]
    GuardianRequest Guardian);

public sealed record StudentResponse(Guid StudentId, string Code, string Identification, string FirstName,
    string LastName, DateOnly BirthDate, string Grade, string SchoolId, GuardianRequest Guardian,
    StudentStatus Status, FinancialStatus FinancialStatus, DateTimeOffset CreatedAt);

public sealed record EnrollmentRequest(
    Guid StudentId,
    [property: Required(ErrorMessage = "schoolYear es obligatorio."),
               MaxLength(20, ErrorMessage = "schoolYear no puede superar 20 caracteres.")]
    string SchoolYear,
    [property: Required(ErrorMessage = "grade es obligatorio."),
               MaxLength(40, ErrorMessage = "grade no puede superar 40 caracteres.")]
    string Grade,
    [property: Required(ErrorMessage = "schoolId es obligatorio."),
               MaxLength(30, ErrorMessage = "schoolId no puede superar 30 caracteres.")]
    string SchoolId);

public sealed record EnrollmentResponse(Guid EnrollmentId, Guid StudentId, string SchoolYear, string Grade,
    string SchoolId, EnrollmentStatus Status, DateTimeOffset EnrolledAt);

public sealed record AcademicEventResponse(Guid EventId, string EventType, string CorrelationId,
    string Payload, DateTimeOffset OccurredAt);
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, long TotalCount);
public sealed record StudentEnrolledData(Guid StudentId, string StudentCode, string FullName, string Grade,
    string SchoolId, string SchoolYear, string GuardianEmail, Guid EnrollmentId);
public sealed record PaymentConfirmedData(Guid PaymentId, Guid DebtId, Guid StudentId, string Concept,
    decimal Amount, string PaymentMethod, DateTimeOffset ConfirmedAt);
public sealed record StudentStatusUpdatedData(Guid StudentId, string PreviousFinancialStatus,
    string NewFinancialStatus, string Reason);
