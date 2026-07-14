namespace AttendanceService.Domain.Entities;

public sealed class ProcessedEvent
{
    public string EventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset ProcessedAt { get; set; }
}
