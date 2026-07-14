namespace SharedKernel.Events;

public sealed record IncidentReportedData(
    Guid IncidentId,
    Guid StudentId,
    string Type,
    string Severity,
    string Description,
    string ReportedBy);
