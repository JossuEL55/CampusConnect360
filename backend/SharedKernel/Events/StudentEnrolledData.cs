namespace SharedKernel.Events;

public sealed record StudentEnrolledData
{
    public Guid StudentId { get; init; }
    public string StudentCode { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Grade { get; init; } = string.Empty;
    public string SchoolId { get; init; } = string.Empty;
    public string SchoolYear { get; init; } = string.Empty;
    public string GuardianEmail { get; init; } = string.Empty;
    public Guid EnrollmentId { get; init; }
}
