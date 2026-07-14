using System.Text.Json;
using AttendanceService.Application.Contracts.Requests;
using AttendanceService.Application.Services;
using AttendanceService.Application.Validation;
using AttendanceService.Domain.Entities;
using AttendanceService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel.Events;
using SharedKernel.Messaging;

namespace AttendanceService.Tests;

public sealed class AttendanceOutboxCreationTests
{
    [Fact]
    public async Task CreateRecord_SavesEntityAndExpectedOutboxEnvelope()
    {
        await using var dbContext = CreateDbContext();
        var student = CreateStudent();
        dbContext.Students.Add(student);
        await dbContext.SaveChangesAsync(
            TestContext.Current.CancellationToken);
        var service = CreateService(dbContext);
        const string correlationId = "record-outbox-correlation";

        var result = await service.CreateRecordAsync(
            new CreateAttendanceRecordRequest(
                student.Id,
                new DateOnly(2026, 7, 15),
                AttendanceStatuses.Absent,
                "Ausencia no justificada"),
            "docente",
            correlationId,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var record = Assert.Single(dbContext.AttendanceRecords);
        var message = Assert.Single(dbContext.OutboxMessages);
        Assert.Equal(correlationId, record.CorrelationId);
        Assert.Equal(correlationId, message.CorrelationId);
        Assert.Equal(EventTypes.AttendanceRecorded, message.EventType);
        Assert.Equal(RoutingKeys.AttendanceRecorded, message.RoutingKey);
        Assert.Equal(0, message.Attempts);
        Assert.Null(message.ProcessedAt);
        Assert.Null(message.LastError);

        using var payload = JsonDocument.Parse(message.Payload);
        var root = payload.RootElement;
        Assert.Equal(message.EventId, root.GetProperty("eventId").GetString());
        Assert.Equal(correlationId,
            root.GetProperty("correlationId").GetString());
        Assert.Equal("AttendanceService",
            root.GetProperty("source").GetString());
        var data = root.GetProperty("data");
        Assert.Equal(record.Id,
            data.GetProperty("recordId").GetGuid());
        Assert.Equal(student.Id,
            data.GetProperty("studentId").GetGuid());
        Assert.Equal("2026-07-15",
            data.GetProperty("date").GetString());
        Assert.Equal("Absent",
            data.GetProperty("status").GetString());
        Assert.Equal("Ausencia no justificada",
            data.GetProperty("remarks").GetString());
        Assert.Equal("docente",
            data.GetProperty("registeredBy").GetString());
    }

    [Fact]
    public async Task CreateIncident_SavesEntityAndExpectedOutboxEnvelope()
    {
        await using var dbContext = CreateDbContext();
        var student = CreateStudent();
        dbContext.Students.Add(student);
        await dbContext.SaveChangesAsync(
            TestContext.Current.CancellationToken);
        var service = CreateService(dbContext);
        const string correlationId = "incident-outbox-correlation";

        var result = await service.CreateIncidentAsync(
            new CreateIncidentRequest(
                student.Id,
                "Wellbeing",
                "High",
                "Estudiante reporta malestar durante la jornada",
                "docente"),
            correlationId,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var incident = Assert.Single(dbContext.Incidents);
        var message = Assert.Single(dbContext.OutboxMessages);
        Assert.Equal(correlationId, incident.CorrelationId);
        Assert.Equal(correlationId, message.CorrelationId);
        Assert.Equal(EventTypes.IncidentReported, message.EventType);
        Assert.Equal(RoutingKeys.IncidentReported, message.RoutingKey);

        using var payload = JsonDocument.Parse(message.Payload);
        var root = payload.RootElement;
        Assert.Equal(message.EventId, root.GetProperty("eventId").GetString());
        Assert.Equal(correlationId,
            root.GetProperty("correlationId").GetString());
        var data = root.GetProperty("data");
        Assert.Equal(incident.Id,
            data.GetProperty("incidentId").GetGuid());
        Assert.Equal(student.Id,
            data.GetProperty("studentId").GetGuid());
        Assert.Equal("Wellbeing", data.GetProperty("type").GetString());
        Assert.Equal("High", data.GetProperty("severity").GetString());
        Assert.Equal(
            "Estudiante reporta malestar durante la jornada",
            data.GetProperty("description").GetString());
        Assert.Equal("docente",
            data.GetProperty("reportedBy").GetString());
    }

    [Fact]
    public async Task SaveFailure_DoesNotPersistRecordOrOutbox()
    {
        var root = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        await using (var seedContext = CreateDbContext(databaseName, root))
        {
            seedContext.Students.Add(CreateStudent());
            await seedContext.SaveChangesAsync(
                TestContext.Current.CancellationToken);
        }

        Guid studentId;
        await using (var readContext = CreateDbContext(databaseName, root))
        {
            studentId = await readContext.Students
                .Select(student => student.Id)
                .SingleAsync(TestContext.Current.CancellationToken);
        }

        await using (var failingContext = CreateDbContext(
            databaseName,
            root,
            new FailingSaveChangesInterceptor()))
        {
            var service = CreateService(failingContext);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.CreateRecordAsync(
                    new CreateAttendanceRecordRequest(
                        studentId,
                        new DateOnly(2026, 7, 15),
                        AttendanceStatuses.Absent,
                        null),
                    "docente",
                    "failure-correlation",
                    TestContext.Current.CancellationToken));
        }

        await using var verificationContext = CreateDbContext(
            databaseName,
            root);
        Assert.Empty(verificationContext.AttendanceRecords);
        Assert.Empty(verificationContext.OutboxMessages);
    }

    private static AttendanceApplicationService CreateService(
        AttendanceDbContext dbContext) => new(
            dbContext,
            TimeProvider.System,
            NullLogger<AttendanceApplicationService>.Instance);

    private static AttendanceDbContext CreateDbContext(
        string? databaseName = null,
        InMemoryDatabaseRoot? root = null,
        IInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<AttendanceDbContext>()
            .UseInMemoryDatabase(
                databaseName ?? Guid.NewGuid().ToString(),
                root)
            .AddInterceptors(interceptor is null ? [] : [interceptor])
            .Options;
        return new AttendanceDbContext(options);
    }

    private static LocalStudent CreateStudent() => new()
    {
        Id = Guid.NewGuid(),
        StudentCode = "STU-OUTBOX",
        FullName = "Estudiante Outbox",
        Grade = "10A",
        SchoolId = "SCHOOL-1",
        SchoolYear = "2026-2027",
        GuardianEmail = "guardian@example.test",
        EnrollmentId = Guid.NewGuid(),
        CreatedAt = DateTimeOffset.UtcNow
    };

    private sealed class FailingSaveChangesInterceptor :
        SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException<InterceptionResult<int>>(
                new InvalidOperationException("Simulated save failure."));
    }
}
