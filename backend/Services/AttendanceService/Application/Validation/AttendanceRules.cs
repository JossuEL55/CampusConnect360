namespace AttendanceService.Application.Validation;

public static class AttendanceStatuses
{
    public const string Present = "Present";
    public const string Absent = "Absent";
    public const string Late = "Late";
    public const string Justified = "Justified";

    private static readonly HashSet<string> Values =
    [Present, Absent, Late, Justified];

    public static bool IsValid(string? value) =>
        value is not null && Values.Contains(value);
}

public static class IncidentTypes
{
    public const string Academic = "Academic";
    public const string Disciplinary = "Disciplinary";
    public const string Wellbeing = "Wellbeing";

    private static readonly HashSet<string> Values =
    [Academic, Disciplinary, Wellbeing];

    public static bool IsValid(string? value) =>
        value is not null && Values.Contains(value);
}

public static class IncidentSeverities
{
    public const string Low = "Low";
    public const string Medium = "Medium";
    public const string High = "High";

    private static readonly HashSet<string> Values =
    [Low, Medium, High];

    public static bool IsValid(string? value) =>
        value is not null && Values.Contains(value);
}

public static class PaginationRules
{
    public const int DefaultPageSize = 20;
    public const int MaximumPageSize = 100;

    public static IReadOnlyDictionary<string, string[]> Validate(
        int page,
        int pageSize)
    {
        var errors = new Dictionary<string, string[]>();

        if (page < 1)
        {
            errors["page"] = ["Page debe ser mayor o igual a 1."];
        }

        if (pageSize < 1 || pageSize > MaximumPageSize)
        {
            errors["pageSize"] =
            ["PageSize debe estar entre 1 y 100."];
        }

        return errors;
    }
}

public static class ValidationLimits
{
    public const int RemarksMaximumLength = 1000;
    public const int DescriptionMaximumLength = 2000;
    public const int ReportedByMaximumLength = 100;
}
