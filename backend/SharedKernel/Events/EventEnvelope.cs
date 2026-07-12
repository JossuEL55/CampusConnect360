namespace SharedKernel.Events;


//Sobre estandar utilizado por todos los eventos de integración de CampusConnect 360.
public sealed record EventEnvelope<TData>
{
    public required Guid EventId { get; init; }

    public required string EventType { get; init; }

    public int Version { get; init; } = 1;

    public required DateTimeOffset OccurredAt { get; init; }

    public required string CorrelationId { get; init; }

    public required string Source { get; init; }

    public required Guid EntityId { get; init; }

    public required TData Data { get; init; }
}