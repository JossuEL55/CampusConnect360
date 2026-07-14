using System.Text.Json;
using System.Text.Json.Serialization;
using PaymentService.Api;
using PaymentService.Messaging;
using SharedKernel.Events;

namespace PaymentService.Tests;

public class PaymentEventContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public void StudentEnrolled_contract_is_validated_before_processing()
    {
        var json = """
        {
          "eventId": "9f1c2e4a-1111-4222-8333-444455556666",
          "eventType": "StudentEnrolled",
          "version": 1,
          "occurredAt": "2026-07-15T10:30:00Z",
          "correlationId": "corr-20260715-001",
          "source": "AcademicService",
          "entityId": "aaaaaaaa-1111-4222-8333-444455556666",
          "data": {
            "studentId": "aaaaaaaa-1111-4222-8333-444455556666",
            "studentCode": "STU-001",
            "fullName": "Ana Torres",
            "grade": "8vo EGB",
            "schoolId": "SCH-001",
            "schoolYear": "2026-2027",
            "guardianEmail": "maria.torres@mail.com",
            "enrollmentId": "bbbbbbbb-1111-4222-8333-444455556666"
          }
        }
        """;

        var envelope = JsonSerializer.Deserialize<EventEnvelope<StudentEnrolledData>>(json, JsonOptions);

        Assert.NotNull(envelope);
        RabbitMqEventBus.ValidateStudentEnrolled(envelope);
        Assert.Equal("StudentEnrolled", envelope.EventType);
        Assert.Equal("STU-001", envelope.Data.StudentCode);
    }

    [Fact]
    public void StudentEnrolled_without_student_id_is_rejected()
    {
        var envelope = EventEnvelopeFactory.Create(
            EventTypes.StudentEnrolled,
            "AcademicService",
            Guid.NewGuid(),
            "corr-test",
            new StudentEnrolledData(
                Guid.Empty,
                "STU-001",
                "Ana Torres",
                "8vo EGB",
                "SCH-001",
                "2026-2027",
                "maria.torres@mail.com",
                Guid.NewGuid()));

        Assert.Throws<JsonException>(() => RabbitMqEventBus.ValidateStudentEnrolled(envelope));
    }

    [Fact]
    public void PaymentConfirmed_payload_matches_integration_contract()
    {
        var data = new PaymentConfirmedData(
            Guid.Parse("c4a91d77-1111-4222-8333-444455556666"),
            Guid.Parse("5e2f8b31-1111-4222-8333-444455556666"),
            Guid.Parse("9f1c2e4a-1111-4222-8333-444455556666"),
            "Matrícula 2026-2027",
            350.00m,
            "Transferencia",
            DateTimeOffset.Parse("2026-07-15T11:05:00Z"));

        var envelope = EventEnvelopeFactory.Create(
            EventTypes.PaymentConfirmed,
            "PaymentService",
            data.StudentId,
            "corr-20260715-001",
            data);
        var json = JsonSerializer.Serialize(envelope, JsonOptions);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal("PaymentConfirmed", root.GetProperty("eventType").GetString());
        Assert.Equal("PaymentService", root.GetProperty("source").GetString());
        Assert.Equal("payments.payment.confirmed", SharedKernel.Messaging.RoutingKeys.PaymentConfirmed);
        Assert.Equal(350.00m, root.GetProperty("data").GetProperty("amount").GetDecimal());
        Assert.Equal("Transferencia", root.GetProperty("data").GetProperty("paymentMethod").GetString());
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
