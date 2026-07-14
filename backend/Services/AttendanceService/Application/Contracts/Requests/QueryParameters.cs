namespace AttendanceService.Application.Contracts.Requests;

public sealed class StudentsQueryParameters
{
    public string? Grade { get; init; }
    public string? Q { get; init; }
    public int? Page { get; init; }
    public int? PageSize { get; init; }
}

public sealed class IncidentsQueryParameters
{
    public string? Severity { get; init; }
    public string? Type { get; init; }
    public Guid? StudentId { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public int? Page { get; init; }
    public int? PageSize { get; init; }
}
