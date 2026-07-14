using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationService.Application.Contracts;
using NotificationService.Application.Services;
using NotificationService.Infrastructure.Persistence;
using SharedKernel.Events;

namespace NotificationService.Tests;

public sealed class NotificationEventProcessorTests
{
    [Fact]
    public async Task StudentEnrolled_CreatesAndUpdatesStudentAndWelcomeArtifacts()
    {
        await using var db = CreateDb();
        var studentId = Guid.NewGuid();
        var first = StudentEnvelope(studentId, "Ana Inicial", "guardian@test.local");
        var processor = CreateProcessor(db);

        var firstResult = await Process(processor, first);
        var updated = StudentEnvelope(studentId, "Ana Actualizada", "new@test.local");
        var secondResult = await Process(processor, updated);

        var student = Assert.Single(db.Students);
        Assert.Equal("Ana Actualizada", student.FullName);
        Assert.Equal("new@test.local", student.GuardianEmail);
        Assert.NotNull(student.UpdatedAt);
        Assert.Equal(NotificationProcessingOutcome.NotificationCreated, firstResult.Outcome);
        Assert.Equal(NotificationProcessingOutcome.NotificationCreated, secondResult.Outcome);
        Assert.Equal(2, db.Notifications.Count());
        Assert.Equal(2, db.NotificationAttempts.Count());
        Assert.Equal(2, db.OutboxMessages.Count());
        Assert.Equal(2, db.ProcessedEvents.Count());

        var notification = db.Notifications.OrderBy(x => x.CreatedAt).First();
        Assert.Equal("Bienvenida: matrícula confirmada", notification.Subject);
        var outbox = db.OutboxMessages.OrderBy(x => x.CreatedAt).First();
        using var payload = JsonDocument.Parse(outbox.Payload);
        var root = payload.RootElement;
        Assert.Equal(EventTypes.NotificationSent, root.GetProperty("eventType").GetString());
        var data = root.GetProperty("data");
        Assert.Equal(notification.Id, data.GetProperty("notificationId").GetGuid());
        Assert.Equal(first.EventId.ToString("D"), data.GetProperty("sourceEventId").GetString());
        Assert.Equal(EventTypes.StudentEnrolled, data.GetProperty("sourceEventType").GetString());
        Assert.Equal("Email", data.GetProperty("channel").GetString());
        Assert.Equal("guardian@test.local", data.GetProperty("recipient").GetString());
        Assert.Equal(1, data.GetProperty("attempts").GetInt32());
    }

    [Fact]
    public async Task Duplicate_DoesNotCreateAnotherNotification()
    {
        await using var db = CreateDb();
        var processor = CreateProcessor(db);
        var envelope = StudentEnvelope(Guid.NewGuid(), "Duplicado", "dup@test.local");
        await Process(processor, envelope);

        var result = await Process(processor, envelope);

        Assert.Equal(NotificationProcessingOutcome.Duplicate, result.Outcome);
        Assert.Single(db.Notifications);
        Assert.Single(db.ProcessedEvents);
    }

    [Fact]
    public async Task PaymentConfirmed_CreatesNotificationWithConceptAndAmount()
    {
        await using var db = CreateDb();
        var student = AddStudent(db);
        var envelope = Envelope(
            EventTypes.PaymentConfirmed,
            student.Id,
            new PaymentConfirmedData(
                Guid.NewGuid(), Guid.NewGuid(), student.Id, "Pensión julio",
                125.50m, "Card", DateTimeOffset.UtcNow));

        await Process(CreateProcessor(db), envelope);

        var notification = Assert.Single(db.Notifications);
        Assert.Contains("Pensión julio", notification.Body);
        Assert.Contains("125.50", notification.Body);
    }

    [Theory]
    [InlineData("Absent")]
    [InlineData("Late")]
    public async Task AlertAttendance_CreatesNotification(string status)
    {
        await using var db = CreateDb();
        var student = AddStudent(db);
        var envelope = Envelope(
            EventTypes.AttendanceRecorded,
            Guid.NewGuid(),
            new AttendanceRecordedData(
                Guid.NewGuid(), student.Id, new DateOnly(2026, 7, 15),
                status, "Detalle", "docente"));

        await Process(CreateProcessor(db), envelope);

        Assert.Single(db.Notifications);
        Assert.Contains(status, db.Notifications.Single().Subject);
    }

    [Theory]
    [InlineData("Present")]
    [InlineData("Justified")]
    public async Task NonAlertAttendance_IsProcessedWithoutNotification(string status)
    {
        await using var db = CreateDb();
        var envelope = Envelope(
            EventTypes.AttendanceRecorded,
            Guid.NewGuid(),
            new AttendanceRecordedData(
                Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 7, 15),
                status, null, "docente"));

        var result = await Process(CreateProcessor(db), envelope);

        Assert.Equal(NotificationProcessingOutcome.Skipped, result.Outcome);
        Assert.Empty(db.Notifications);
        Assert.Single(db.ProcessedEvents);
    }

    [Theory]
    [InlineData("Medium")]
    [InlineData("High")]
    public async Task AlertIncident_CreatesNotification(string severity)
    {
        await using var db = CreateDb();
        var student = AddStudent(db);
        var envelope = Envelope(
            EventTypes.IncidentReported,
            Guid.NewGuid(),
            new IncidentReportedData(
                Guid.NewGuid(), student.Id, "Wellbeing", severity,
                "Descripción", "docente"));

        await Process(CreateProcessor(db), envelope);

        Assert.Single(db.Notifications);
        Assert.Contains(severity, db.Notifications.Single().Subject);
    }

    [Fact]
    public async Task LowIncident_IsProcessedWithoutNotification()
    {
        await using var db = CreateDb();
        var envelope = Envelope(
            EventTypes.IncidentReported,
            Guid.NewGuid(),
            new IncidentReportedData(
                Guid.NewGuid(), Guid.NewGuid(), "Academic", "Low",
                "Descripción", "docente"));

        var result = await Process(CreateProcessor(db), envelope);

        Assert.Equal(NotificationProcessingOutcome.Skipped, result.Outcome);
        Assert.Empty(db.Notifications);
        Assert.Single(db.ProcessedEvents);
    }

    [Fact]
    public async Task MissingStudent_ThrowsControlledExceptionAndDoesNotProcess()
    {
        await using var db = CreateDb();
        var studentId = Guid.NewGuid();
        var envelope = Envelope(
            EventTypes.AttendanceRecorded,
            Guid.NewGuid(),
            new AttendanceRecordedData(
                Guid.NewGuid(), studentId, new DateOnly(2026, 7, 15),
                "Absent", null, "docente"));

        var exception = await Assert.ThrowsAsync<StudentReplicaNotFoundException>(
            () => Process(CreateProcessor(db), envelope));

        Assert.Equal(studentId, exception.StudentId);
        Assert.Empty(db.ProcessedEvents);
        Assert.Empty(db.Notifications);
    }

    private static NotificationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new(options);
    }

    private static NotificationEventProcessor CreateProcessor(NotificationDbContext db) =>
        new(db, TimeProvider.System,
            NullLogger<NotificationEventProcessor>.Instance);

    private static async Task<NotificationProcessingResult> Process<T>(
        NotificationEventProcessor processor,
        EventEnvelope<T> envelope) => await processor.ProcessAsync(
            JsonSerializer.SerializeToUtf8Bytes(
                envelope,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            TestContext.Current.CancellationToken);

    private static EventEnvelope<StudentEnrolledData> StudentEnvelope(
        Guid studentId,
        string fullName,
        string email) => Envelope(
            EventTypes.StudentEnrolled,
            studentId,
            new StudentEnrolledData
            {
                StudentId = studentId,
                StudentCode = "STU-001",
                FullName = fullName,
                Grade = "10A",
                SchoolId = "SCH-1",
                SchoolYear = "2026-2027",
                GuardianEmail = email,
                EnrollmentId = Guid.NewGuid()
            });

    private static EventEnvelope<T> Envelope<T>(string type, Guid entityId, T data) =>
        EventEnvelopeFactory.Create(type, "TestService", entityId,
            $"test-{Guid.NewGuid():N}", data);

    private static Domain.Entities.LocalStudent AddStudent(NotificationDbContext db)
    {
        var student = new Domain.Entities.LocalStudent
        {
            Id = Guid.NewGuid(), StudentCode = "STU-LOCAL",
            FullName = "Estudiante Local", Grade = "10A", SchoolId = "SCH-1",
            SchoolYear = "2026-2027", GuardianEmail = "guardian@test.local",
            EnrollmentId = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow
        };
        db.Students.Add(student);
        db.SaveChanges();
        return student;
    }
}
