namespace AttendanceService.Application.Contracts.Requests;

public sealed record CreateAttendanceRecordRequest(
    Guid StudentId,
    DateOnly? Date,
    string? Status,
    string? Remarks);
