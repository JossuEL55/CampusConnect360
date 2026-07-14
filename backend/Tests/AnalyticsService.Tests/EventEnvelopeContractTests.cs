using System.Text.Json;
using SharedKernel.Events;

namespace AnalyticsService.Tests;

// Fija el formato del sobre del contrato (10.1) tal como lo deserializa el consumidor.
public class EventEnvelopeContractTests
{
    private static readonly JsonSerializerOptions ConsumerOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public void DeserializaElSobreCamelCaseDelContrato()
    {
        const string json = """
        {
          "eventId": "9f1c2e4a-1111-4222-8333-444455556666",
          "eventType": "StudentEnrolled",
          "version": 1,
          "occurredAt": "2026-07-15T10:30:00Z",
          "correlationId": "corr-20260715-001",
          "source": "AcademicService",
          "entityId": "9f1c2e4a-1111-4222-8333-444455556666",
          "data": { "studentCode": "STU-001" }
        }
        """;

        var envelope = JsonSerializer.Deserialize<EventEnvelope<JsonElement>>(json, ConsumerOptions);

        Assert.NotNull(envelope);
        Assert.Equal("StudentEnrolled", envelope.EventType);
        Assert.Equal("corr-20260715-001", envelope.CorrelationId);
        Assert.Equal(new DateTimeOffset(2026, 7, 15, 10, 30, 0, TimeSpan.Zero), envelope.OccurredAt);
        Assert.Equal("STU-001", envelope.Data.GetProperty("studentCode").GetString());
    }

    [Fact]
    public void DeserializaTambienSobresEnPascalCase()
    {
        const string json = """
        {
          "EventId": "9f1c2e4a-1111-4222-8333-444455556666",
          "EventType": "PaymentConfirmed",
          "Version": 1,
          "OccurredAt": "2026-07-15T11:05:00Z",
          "CorrelationId": "corr-001",
          "Source": "PaymentService",
          "EntityId": "9f1c2e4a-1111-4222-8333-444455556666",
          "Data": { "amount": 350.00 }
        }
        """;

        var envelope = JsonSerializer.Deserialize<EventEnvelope<JsonElement>>(json, ConsumerOptions);

        Assert.NotNull(envelope);
        Assert.Equal("PaymentConfirmed", envelope.EventType);
    }

    [Fact]
    public void SobreSinCamposObligatoriosLanzaJsonException()
    {
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<EventEnvelope<JsonElement>>("""{"eventType":"X"}""", ConsumerOptions));
    }
}
