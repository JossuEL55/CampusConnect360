namespace SharedKernel.Events;

public sealed record AttendanceRecordedData(
    Guid RecordId,
    Guid StudentId,
    DateOnly Date,
    string Status,
    string? Remarks,
    string RegisteredBy);
