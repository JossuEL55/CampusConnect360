using AttendanceService.Application.Services;
using AttendanceService.Domain.Entities;
using AttendanceService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel.Events;

namespace AttendanceService.Tests;

public sealed class StudentEnrollmentProjectionServiceTests
{
    [Fact]
    public async Task NewEvent_CreatesStudentAndProcessedEvent()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var envelope = CreateEnvelope();

        var result = await service.ProjectAsync(
            envelope,
            CancellationToken.None);

        Assert.Equal(StudentEnrollmentProjectionOutcome.Created, result.Outcome);
        var student = Assert.Single(dbContext.Students);
        Assert.Equal(envelope.Data.StudentId, student.Id);
        Assert.Equal(envelope.Data.FullName, student.FullName);
        var processedEvent = Assert.Single(dbContext.ProcessedEvents);
        Assert.Equal(envelope.EventId.ToString("D"), processedEvent.EventId);
    }

    [Fact]
    public async Task DuplicateEvent_DoesNotModifyOrDuplicateStudent()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var envelope = CreateEnvelope();
        await service.ProjectAsync(envelope, CancellationToken.None);

        var duplicate = envelope with
        {
            Data = envelope.Data with { FullName = "Changed Name" }
        };
        var result = await service.ProjectAsync(
            duplicate,
            CancellationToken.None);

        Assert.Equal(StudentEnrollmentProjectionOutcome.Duplicate, result.Outcome);
        Assert.Equal("Student Test", Assert.Single(dbContext.Students).FullName);
        Assert.Single(dbContext.ProcessedEvents);
    }

    [Fact]
    public async Task ExistingStudent_IsUpdatedWithoutChangingCreatedAt()
    {
        await using var dbContext = CreateDbContext();
        var originalCreatedAt = new DateTimeOffset(
            2025,
            1,
            1,
            0,
            0,
            0,
            TimeSpan.Zero);
        var envelope = CreateEnvelope();
        dbContext.Students.Add(new LocalStudent
        {
            Id = envelope.Data.StudentId,
            StudentCode = "OLD",
            FullName = "Old Name",
            Grade = "Old Grade",
            SchoolId = "OLD-SCHOOL",
            SchoolYear = "2025",
            GuardianEmail = "old@example.test",
            EnrollmentId = Guid.NewGuid(),
            CreatedAt = originalCreatedAt
        });
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(dbContext);

        var result = await service.ProjectAsync(
            envelope,
            CancellationToken.None);

        Assert.Equal(StudentEnrollmentProjectionOutcome.Updated, result.Outcome);
        var student = Assert.Single(dbContext.Students);
        Assert.Equal(envelope.Data.FullName, student.FullName);
        Assert.Equal(envelope.Data.EnrollmentId, student.EnrollmentId);
        Assert.Equal(originalCreatedAt, student.CreatedAt);
        Assert.NotNull(student.UpdatedAt);
        Assert.Single(dbContext.ProcessedEvents);
    }

    [Fact]
    public async Task IncorrectEventType_IsRejected()
    {
        await AssertInvalidAsync(CreateEnvelope() with
        {
            EventType = EventTypes.PaymentConfirmed
        });
    }

    [Fact]
    public async Task EmptyEventId_IsRejected()
    {
        await AssertInvalidAsync(CreateEnvelope() with
        {
            EventId = Guid.Empty
        });
    }

    [Fact]
    public async Task NullData_IsRejected()
    {
        await AssertInvalidAsync(CreateEnvelope() with
        {
            Data = null!
        });
    }

    [Fact]
    public async Task EmptyStudentId_IsRejected()
    {
        var envelope = CreateEnvelope();
        await AssertInvalidAsync(envelope with
        {
            EntityId = Guid.Empty,
            Data = envelope.Data with { StudentId = Guid.Empty }
        });
    }

    [Fact]
    public async Task EmptyEnrollmentId_IsRejected()
    {
        var envelope = CreateEnvelope();
        await AssertInvalidAsync(envelope with
        {
            Data = envelope.Data with { EnrollmentId = Guid.Empty }
        });
    }

    [Fact]
    public async Task EnrollmentOwnedByAnotherStudent_IsRejected()
    {
        await using var dbContext = CreateDbContext();
        var envelope = CreateEnvelope();
        var existingStudentId = Guid.NewGuid();
        dbContext.Students.Add(new LocalStudent
        {
            Id = existingStudentId,
            StudentCode = "EXISTING",
            FullName = "Existing Student",
            Grade = "9A",
            SchoolId = "SCHOOL-1",
            SchoolYear = "2026-2027",
            GuardianEmail = "existing@example.test",
            EnrollmentId = envelope.Data.EnrollmentId,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(dbContext);

        var exception = await Assert.ThrowsAsync<
            StudentEnrollmentConflictException>(() =>
            service.ProjectAsync(envelope, CancellationToken.None));

        Assert.Equal(existingStudentId, exception.ExistingStudentId);
        Assert.Single(dbContext.Students);
        Assert.Empty(dbContext.ProcessedEvents);
    }

    [Fact]
    public async Task FailureBeforeSave_DoesNotPersistProcessedEvent()
    {
        var databaseRoot = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        var failingOptions = CreateOptions(
            databaseName,
            databaseRoot,
            new ThrowBeforeSaveInterceptor());

        await using (var failingContext =
                     new AttendanceDbContext(failingOptions))
        {
            var service = CreateService(failingContext);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ProjectAsync(
                    CreateEnvelope(),
                    CancellationToken.None));
        }

        var verificationOptions = CreateOptions(databaseName, databaseRoot);
        await using var verificationContext =
            new AttendanceDbContext(verificationOptions);

        Assert.Empty(verificationContext.Students);
        Assert.Empty(verificationContext.ProcessedEvents);
    }

    private static async Task AssertInvalidAsync(
        EventEnvelope<StudentEnrolledData> envelope)
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        await Assert.ThrowsAsync<InvalidStudentEnrolledEventException>(() =>
            service.ProjectAsync(envelope, CancellationToken.None));

        Assert.Empty(dbContext.Students);
        Assert.Empty(dbContext.ProcessedEvents);
    }

    private static StudentEnrollmentProjectionService CreateService(
        AttendanceDbContext dbContext) =>
        new(
            dbContext,
            TimeProvider.System,
            NullLogger<StudentEnrollmentProjectionService>.Instance);

    private static AttendanceDbContext CreateDbContext()
    {
        var options = CreateOptions(
            Guid.NewGuid().ToString(),
            new InMemoryDatabaseRoot());

        return new AttendanceDbContext(options);
    }

    private static DbContextOptions<AttendanceDbContext> CreateOptions(
        string databaseName,
        InMemoryDatabaseRoot databaseRoot,
        ISaveChangesInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<AttendanceDbContext>()
            .UseInMemoryDatabase(databaseName, databaseRoot);

        if (interceptor is not null)
        {
            builder.AddInterceptors(interceptor);
        }

        return builder.Options;
    }

    private static EventEnvelope<StudentEnrolledData> CreateEnvelope()
    {
        var studentId = Guid.NewGuid();

        return new EventEnvelope<StudentEnrolledData>
        {
            EventId = Guid.NewGuid(),
            EventType = EventTypes.StudentEnrolled,
            Version = 1,
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = "test-correlation-id",
            Source = "AcademicService",
            EntityId = studentId,
            Data = new StudentEnrolledData
            {
                StudentId = studentId,
                StudentCode = "STU-TEST",
                FullName = "Student Test",
                Grade = "8A",
                SchoolId = "SCHOOL-1",
                SchoolYear = "2026-2027",
                GuardianEmail = "guardian@example.test",
                EnrollmentId = Guid.NewGuid()
            }
        };
    }

    private sealed class ThrowBeforeSaveInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated persistence failure.");
    }
}
