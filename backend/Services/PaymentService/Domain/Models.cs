namespace PaymentService.Domain;

public enum DebtStatus { Pending, Paid, Overdue }
public enum PaymentStatus { Confirmed }

public sealed class PaymentStudent
{
    public Guid Id { get; set; }
    public required string StudentCode { get; set; }
    public required string FullName { get; set; }
    public required string Grade { get; set; }
    public required string SchoolId { get; set; }
    public required string SchoolYear { get; set; }
    public required string GuardianEmail { get; set; }
    public Guid EnrollmentId { get; set; }
    public DateTimeOffset EnrolledAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class Debt
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public required string Concept { get; set; }
    public decimal Amount { get; set; }
    public DateOnly DueDate { get; set; }
    public DebtStatus Status { get; set; } = DebtStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public PaymentStudent? Student { get; set; }
}

public sealed class Payment
{
    public Guid Id { get; set; }
    public Guid DebtId { get; set; }
    public Guid StudentId { get; set; }
    public decimal Amount { get; set; }
    public required string PaymentMethod { get; set; }
    public required string Reference { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Confirmed;
    public DateTimeOffset ConfirmedAt { get; set; }
    public Debt? Debt { get; set; }
}

public sealed class PaymentEvent
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public required string EventType { get; set; }
    public required string SourceEventId { get; set; }
    public required string CorrelationId { get; set; }
    public required string Payload { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
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
