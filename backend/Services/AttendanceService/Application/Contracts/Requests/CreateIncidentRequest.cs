namespace AttendanceService.Application.Contracts.Requests;

public sealed record CreateIncidentRequest(
    Guid StudentId,
    string? Type,
    string? Severity,
    string? Description,
    string? ReportedBy);
