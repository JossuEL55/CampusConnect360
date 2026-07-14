namespace AttendanceService.Domain.Entities;

public sealed class LocalStudent
{
    public Guid Id { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Grade { get; set; } = string.Empty;
    public string SchoolId { get; set; } = string.Empty;
    public string SchoolYear { get; set; } = string.Empty;
    public string GuardianEmail { get; set; } = string.Empty;
    public Guid EnrollmentId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public ICollection<AttendanceRecord> AttendanceRecords { get; } = [];
    public ICollection<Incident> Incidents { get; } = [];
}
