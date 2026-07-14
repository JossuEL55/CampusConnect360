namespace AttendanceService.Application.Contracts.Responses;

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record StudentListItemResponse(
    Guid Id,
    string StudentCode,
    string FullName,
    string Grade,
    string SchoolId,
    string SchoolYear,
    string GuardianEmail,
    Guid EnrollmentId);

public sealed record AttendanceRecordResponse(
    Guid Id,
    Guid StudentId,
    DateOnly Date,
    string Status,
    string? Remarks,
    string RegisteredBy,
    string CorrelationId,
    DateTimeOffset CreatedAt);

public sealed record IncidentResponse(
    Guid Id,
    Guid StudentId,
    string Type,
    string Severity,
    string Description,
    string ReportedBy,
    string CorrelationId,
    DateTimeOffset CreatedAt);

public sealed record StudentBasicResponse(
    Guid Id,
    string StudentCode,
    string FullName,
    string Grade);

public sealed record StudentHistoryItemResponse(
    string Type,
    DateTimeOffset OccurredAt,
    DateOnly? Date,
    string? Status,
    string? Remarks,
    string? IncidentType,
    string? Severity,
    string? Description);

public sealed record StudentHistoryResponse(
    StudentBasicResponse Student,
    IReadOnlyList<StudentHistoryItemResponse> Items);
