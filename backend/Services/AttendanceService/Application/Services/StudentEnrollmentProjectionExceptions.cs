namespace AttendanceService.Application.Services;

public sealed class InvalidStudentEnrolledEventException(
    IReadOnlyList<string> errors) : Exception(
        "The StudentEnrolled event contract is invalid.")
{
    public IReadOnlyList<string> Errors { get; } = errors;
}

public sealed class StudentEnrollmentConflictException(
    Guid enrollmentId,
    Guid existingStudentId,
    Guid receivedStudentId) : Exception(
        $"EnrollmentId {enrollmentId} belongs to student " +
        $"{existingStudentId}, not {receivedStudentId}.")
{
    public Guid EnrollmentId { get; } = enrollmentId;
    public Guid ExistingStudentId { get; } = existingStudentId;
    public Guid ReceivedStudentId { get; } = receivedStudentId;
}
