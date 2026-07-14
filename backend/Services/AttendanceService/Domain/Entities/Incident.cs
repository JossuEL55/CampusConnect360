namespace AttendanceService.Domain.Entities;

public sealed class Incident
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ReportedBy { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public LocalStudent Student { get; set; } = null!;
}
