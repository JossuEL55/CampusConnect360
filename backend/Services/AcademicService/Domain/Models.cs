namespace AcademicService.Domain;

public enum StudentStatus { Registered, Active, Inactive }
public enum FinancialStatus { NoDebt, Pending, UpToDate, Overdue }
public enum EnrollmentStatus { Confirmed, Cancelled }

public sealed class Student
{
    public Guid Id { get; set; }
    public required string Identification { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public DateOnly BirthDate { get; set; }
    public required string Grade { get; set; }
    public required string SchoolId { get; set; }
    public required string GuardianFullName { get; set; }
    public required string GuardianEmail { get; set; }
    public required string GuardianPhone { get; set; }
    public required string Code { get; set; }
    public StudentStatus Status { get; set; } = StudentStatus.Registered;
    public FinancialStatus FinancialStatus { get; set; } = FinancialStatus.NoDebt;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class Enrollment
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public required string SchoolYear { get; set; }
    public required string Grade { get; set; }
    public required string SchoolId { get; set; }
    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Confirmed;
    public DateTimeOffset EnrolledAt { get; set; }
    public Student? Student { get; set; }
}

public sealed class AcademicEvent
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public required string EventType { get; set; }
    public required string SourceEventId { get; set; }
    public required string CorrelationId { get; set; }
    public required string Payload { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public Student? Student { get; set; }
}

public sealed class ProcessedEvent
{
    public Guid EventId { get; set; }
    public required string EventType { get; set; }
    public DateTimeOffset ProcessedAt { get; set; }
}

public sealed class OutboxMessage
{
    public Guid EventId { get; set; }
    public required string EventType { get; set; }
    public required string RoutingKey { get; set; }
    public required string CorrelationId { get; set; }
    public required string Payload { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset? DispatchedAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}
