using SharedKernel.Events;

namespace AttendanceService.Application.Validation;

public sealed record EventValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors);

public static class StudentEnrolledEventValidator
{
    public const int SupportedVersion = 1;

    public static EventValidationResult Validate(
        EventEnvelope<StudentEnrolledData>? envelope)
    {
        var errors = new List<string>();

        if (envelope is null)
        {
            errors.Add("Event envelope is required.");
            return new EventValidationResult(false, errors);
        }

        if (envelope.EventId == Guid.Empty)
        {
            errors.Add("EventId is required.");
        }

        if (envelope.EventType != EventTypes.StudentEnrolled)
        {
            errors.Add($"EventType must be {EventTypes.StudentEnrolled}.");
        }

        if (envelope.Version != SupportedVersion)
        {
            errors.Add($"Version {envelope.Version} is not supported.");
        }

        if (string.IsNullOrWhiteSpace(envelope.CorrelationId))
        {
            errors.Add("CorrelationId is required.");
        }

        if (string.IsNullOrWhiteSpace(envelope.Source))
        {
            errors.Add("Source is required.");
        }

        if (envelope.EntityId == Guid.Empty)
        {
            errors.Add("EntityId is required.");
        }

        if (envelope.Data is null)
        {
            errors.Add("Data is required.");
            return new EventValidationResult(false, errors);
        }

        if (envelope.Data.StudentId == Guid.Empty)
        {
            errors.Add("StudentId is required.");
        }

        if (envelope.EntityId != Guid.Empty &&
            envelope.Data.StudentId != Guid.Empty &&
            envelope.EntityId != envelope.Data.StudentId)
        {
            errors.Add("EntityId must match Data.StudentId.");
        }

        if (envelope.Data.EnrollmentId == Guid.Empty)
        {
            errors.Add("EnrollmentId is required.");
        }

        ValidateRequired(envelope.Data.StudentCode, "StudentCode", errors);
        ValidateRequired(envelope.Data.FullName, "FullName", errors);
        ValidateRequired(envelope.Data.Grade, "Grade", errors);
        ValidateRequired(envelope.Data.SchoolId, "SchoolId", errors);
        ValidateRequired(envelope.Data.SchoolYear, "SchoolYear", errors);
        ValidateRequired(
            envelope.Data.GuardianEmail,
            "GuardianEmail",
            errors);

        return new EventValidationResult(errors.Count == 0, errors);
    }

    private static void ValidateRequired(
        string? value,
        string fieldName,
        ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{fieldName} is required.");
        }
    }
}
