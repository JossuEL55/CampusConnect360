using AttendanceService.Application.Validation;
using AttendanceService.Domain.Entities;
using AttendanceService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SharedKernel.Events;

namespace AttendanceService.Application.Services;

public sealed class StudentEnrollmentProjectionService(
    AttendanceDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<StudentEnrollmentProjectionService> logger) :
    IStudentEnrollmentProjectionService
{
    public async Task<StudentEnrollmentProjectionResult> ProjectAsync(
        EventEnvelope<StudentEnrolledData> envelope,
        CancellationToken cancellationToken)
    {
        var validation = StudentEnrolledEventValidator.Validate(envelope);
        if (!validation.IsValid)
        {
            throw new InvalidStudentEnrolledEventException(
                validation.Errors);
        }

        IDbContextTransaction? transaction = null;

        try
        {
            if (dbContext.Database.IsRelational())
            {
                transaction = await dbContext.Database
                    .BeginTransactionAsync(cancellationToken);
            }

            var eventId = envelope.EventId.ToString("D");
            var alreadyProcessed = await dbContext.ProcessedEvents
                .AnyAsync(
                    processedEvent => processedEvent.EventId == eventId,
                    cancellationToken);

            if (alreadyProcessed)
            {
                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                logger.LogInformation(
                    "Duplicate event {EventId} ignored for student {StudentId}. " +
                    "EventType={EventType} CorrelationId={CorrelationId} " +
                    "ServiceName={ServiceName}",
                    envelope.EventId,
                    envelope.Data.StudentId,
                    envelope.EventType,
                    envelope.CorrelationId,
                    "AttendanceService");

                return new StudentEnrollmentProjectionResult(
                    StudentEnrollmentProjectionOutcome.Duplicate,
                    envelope.Data.StudentId);
            }

            var enrollmentOwner = await dbContext.Students
                .AsNoTracking()
                .Where(student =>
                    student.EnrollmentId == envelope.Data.EnrollmentId &&
                    student.Id != envelope.Data.StudentId)
                .Select(student => (Guid?)student.Id)
                .SingleOrDefaultAsync(cancellationToken);

            if (enrollmentOwner.HasValue)
            {
                throw new StudentEnrollmentConflictException(
                    envelope.Data.EnrollmentId,
                    enrollmentOwner.Value,
                    envelope.Data.StudentId);
            }

            var now = timeProvider.GetUtcNow();
            var student = await dbContext.Students
                .SingleOrDefaultAsync(
                    candidate => candidate.Id == envelope.Data.StudentId,
                    cancellationToken);

            StudentEnrollmentProjectionOutcome outcome;

            if (student is null)
            {
                student = new LocalStudent
                {
                    Id = envelope.Data.StudentId,
                    CreatedAt = now
                };
                ApplyProjection(student, envelope.Data);
                dbContext.Students.Add(student);
                outcome = StudentEnrollmentProjectionOutcome.Created;
            }
            else
            {
                ApplyProjection(student, envelope.Data);
                student.UpdatedAt = now;
                outcome = StudentEnrollmentProjectionOutcome.Updated;
            }

            dbContext.ProcessedEvents.Add(new ProcessedEvent
            {
                EventId = eventId,
                EventType = envelope.EventType,
                CorrelationId = envelope.CorrelationId,
                ProcessedAt = now
            });

            await dbContext.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            logger.LogInformation(
                "Student {StudentId} {ProjectionOutcome} from event {EventId}. " +
                "EventType={EventType} CorrelationId={CorrelationId} " +
                "ServiceName={ServiceName}",
                student.Id,
                outcome,
                envelope.EventId,
                envelope.EventType,
                envelope.CorrelationId,
                "AttendanceService");

            return new StudentEnrollmentProjectionResult(outcome, student.Id);
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }

            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    private static void ApplyProjection(
        LocalStudent student,
        StudentEnrolledData data)
    {
        student.StudentCode = data.StudentCode;
        student.FullName = data.FullName;
        student.Grade = data.Grade;
        student.SchoolId = data.SchoolId;
        student.SchoolYear = data.SchoolYear;
        student.GuardianEmail = data.GuardianEmail;
        student.EnrollmentId = data.EnrollmentId;
    }
}
