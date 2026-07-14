using SharedKernel.Events;

namespace AttendanceService.Application.Services;

public enum StudentEnrollmentProjectionOutcome
{
    Created,
    Updated,
    Duplicate
}

public sealed record StudentEnrollmentProjectionResult(
    StudentEnrollmentProjectionOutcome Outcome,
    Guid StudentId);

public interface IStudentEnrollmentProjectionService
{
    Task<StudentEnrollmentProjectionResult> ProjectAsync(
        EventEnvelope<StudentEnrolledData> envelope,
        CancellationToken cancellationToken);
}
