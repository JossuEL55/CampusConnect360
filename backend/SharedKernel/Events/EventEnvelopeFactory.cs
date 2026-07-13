namespace SharedKernel.Events;

/// Facilita la creación uniforme de eventos de integración.
public static class EventEnvelopeFactory
{
    public static EventEnvelope<TData> Create<TData>(
        string eventType,
        string source,
        Guid entityId,
        string correlationId,
        TData data,
        int version = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentNullException.ThrowIfNull(data);

        return new EventEnvelope<TData>
        {
            EventId = Guid.NewGuid(),
            EventType = eventType,
            Version = version,
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = correlationId,
            Source = source,
            EntityId = entityId,
            Data = data
        };
    }
}