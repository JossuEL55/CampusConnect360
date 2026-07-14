namespace AttendanceService.Domain.Entities;

public sealed class AttendanceRecord
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public DateOnly Date { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Remarks { get; set; }
    public string RegisteredBy { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public LocalStudent Student { get; set; } = null!;
}
