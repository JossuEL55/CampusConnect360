using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NotificationService.Application.Contracts;
using NotificationService.Domain.Entities;
using NotificationService.Infrastructure.Persistence;
using SharedKernel.Events;
using NotificationEntity = NotificationService.Domain.Entities.Notification;

namespace NotificationService.Application.Services;

public sealed class NotificationEventProcessor(
    NotificationDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<NotificationEventProcessor> logger)
{
    private const string ServiceName = "NotificationService";
    private const string Channel = "Email";
    private static readonly TimeSpan FirstAttemptDelay = TimeSpan.FromSeconds(5);
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

    public async Task<NotificationProcessingResult> ProcessAsync(
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken)
    {
        EventEnvelope<JsonElement> header;
        try
        {
            header = JsonSerializer.Deserialize<EventEnvelope<JsonElement>>(
                body.Span,
                JsonOptions) ?? throw new JsonException("Event envelope is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidNotificationEventException(
                $"Invalid JSON envelope: {exception.Message}");
        }

        ValidateEnvelope(header);

        IDbContextTransaction? transaction = null;
        try
        {
            if (dbContext.Database.IsRelational())
            {
                transaction = await dbContext.Database
                    .BeginTransactionAsync(cancellationToken);
            }

            var eventId = header.EventId.ToString("D");
            if (await dbContext.ProcessedEvents.AnyAsync(
                    x => x.EventId == eventId,
                    cancellationToken))
            {
                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                logger.LogInformation(
                    "Duplicate notification source event ignored. " +
                    "EventId={EventId} EventType={EventType} " +
                    "CorrelationId={CorrelationId} ServiceName={ServiceName}",
                    header.EventId,
                    header.EventType,
                    header.CorrelationId,
                    ServiceName);
                return new(NotificationProcessingOutcome.Duplicate);
            }

            var result = header.EventType switch
            {
                EventTypes.StudentEnrolled => await ProcessStudentEnrolledAsync(
                    Deserialize<StudentEnrolledData>(body),
                    cancellationToken),
                EventTypes.PaymentConfirmed => await ProcessPaymentConfirmedAsync(
                    Deserialize<PaymentConfirmedData>(body),
                    cancellationToken),
                EventTypes.AttendanceRecorded => await ProcessAttendanceRecordedAsync(
                    Deserialize<AttendanceRecordedData>(body),
                    cancellationToken),
                EventTypes.IncidentReported => await ProcessIncidentReportedAsync(
                    Deserialize<IncidentReportedData>(body),
                    cancellationToken),
                _ => throw new InvalidNotificationEventException(
                    $"EventType {header.EventType} is not supported.")
            };

            dbContext.ProcessedEvents.Add(new ProcessedEvent
            {
                EventId = eventId,
                EventType = header.EventType,
                CorrelationId = header.CorrelationId,
                ProcessedAt = timeProvider.GetUtcNow()
            });

            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            logger.LogInformation(
                "Notification source event processed. EventId={EventId} " +
                "EventType={EventType} CorrelationId={CorrelationId} " +
                "Outcome={Outcome} ServiceName={ServiceName}",
                header.EventId,
                header.EventType,
                header.CorrelationId,
                result.Outcome,
                ServiceName);
            return result;
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

    private async Task<NotificationProcessingResult> ProcessStudentEnrolledAsync(
        EventEnvelope<StudentEnrolledData> envelope,
        CancellationToken cancellationToken)
    {
        var data = envelope.Data;
        if (data.StudentId == Guid.Empty || data.EnrollmentId == Guid.Empty ||
            envelope.EntityId != data.StudentId ||
            HasBlank(data.StudentCode, data.FullName, data.Grade, data.SchoolId,
                data.SchoolYear, data.GuardianEmail))
        {
            throw new InvalidNotificationEventException(
                "StudentEnrolled does not satisfy the integration contract.");
        }

        var enrollmentOwner = await dbContext.Students.AsNoTracking()
            .Where(x => x.EnrollmentId == data.EnrollmentId && x.Id != data.StudentId)
            .Select(x => (Guid?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (enrollmentOwner.HasValue)
        {
            throw new InvalidNotificationEventException(
                "EnrollmentId belongs to another student.");
        }

        var now = timeProvider.GetUtcNow();
        var student = await dbContext.Students.SingleOrDefaultAsync(
            x => x.Id == data.StudentId,
            cancellationToken);
        if (student is null)
        {
            student = new LocalStudent { Id = data.StudentId, CreatedAt = now };
            dbContext.Students.Add(student);
        }
        else
        {
            student.UpdatedAt = now;
        }

        student.StudentCode = data.StudentCode;
        student.FullName = data.FullName;
        student.Grade = data.Grade;
        student.SchoolId = data.SchoolId;
        student.SchoolYear = data.SchoolYear;
        student.GuardianEmail = data.GuardianEmail;
        student.EnrollmentId = data.EnrollmentId;

        return CreateNotification(
            envelope,
            student.Id,
            student.GuardianEmail,
            "Bienvenida: matrícula confirmada",
            $"La matrícula de {student.FullName} para {student.Grade}, " +
            $"año lectivo {student.SchoolYear}, fue confirmada.");
    }

    private async Task<NotificationProcessingResult> ProcessPaymentConfirmedAsync(
        EventEnvelope<PaymentConfirmedData> envelope,
        CancellationToken cancellationToken)
    {
        var data = envelope.Data;
        if (data.PaymentId == Guid.Empty || data.StudentId == Guid.Empty ||
            string.IsNullOrWhiteSpace(data.Concept) || data.Amount < 0)
        {
            throw new InvalidNotificationEventException(
                "PaymentConfirmed does not satisfy the integration contract.");
        }

        var student = await GetStudentAsync(data.StudentId, cancellationToken);
        return CreateNotification(
            envelope,
            student.Id,
            student.GuardianEmail,
            "Confirmación de pago",
            $"Se confirmó el pago de {data.Concept} por " +
            $"{data.Amount.ToString("0.00", CultureInfo.InvariantCulture)}.");
    }

    private async Task<NotificationProcessingResult> ProcessAttendanceRecordedAsync(
        EventEnvelope<AttendanceRecordedData> envelope,
        CancellationToken cancellationToken)
    {
        var data = envelope.Data;
        if (data.RecordId == Guid.Empty || data.StudentId == Guid.Empty ||
            string.IsNullOrWhiteSpace(data.Status) ||
            string.IsNullOrWhiteSpace(data.RegisteredBy))
        {
            throw new InvalidNotificationEventException(
                "AttendanceRecorded does not satisfy the integration contract.");
        }

        if (data.Status is "Present" or "Justified")
        {
            return new(NotificationProcessingOutcome.Skipped);
        }

        if (data.Status is not ("Absent" or "Late"))
        {
            throw new InvalidNotificationEventException(
                $"Attendance status {data.Status} is not supported.");
        }

        var student = await GetStudentAsync(data.StudentId, cancellationToken);
        return CreateNotification(
            envelope,
            student.Id,
            student.GuardianEmail,
            $"Alerta de asistencia: {data.Status}",
            $"Se registró el estado {data.Status} para {student.FullName} " +
            $"el {data.Date:yyyy-MM-dd}. {data.Remarks}".Trim());
    }

    private async Task<NotificationProcessingResult> ProcessIncidentReportedAsync(
        EventEnvelope<IncidentReportedData> envelope,
        CancellationToken cancellationToken)
    {
        var data = envelope.Data;
        if (data.IncidentId == Guid.Empty || data.StudentId == Guid.Empty ||
            HasBlank(data.Type, data.Severity, data.Description, data.ReportedBy))
        {
            throw new InvalidNotificationEventException(
                "IncidentReported does not satisfy the integration contract.");
        }

        if (data.Severity == "Low")
        {
            return new(NotificationProcessingOutcome.Skipped);
        }

        if (data.Severity is not ("Medium" or "High"))
        {
            throw new InvalidNotificationEventException(
                $"Incident severity {data.Severity} is not supported.");
        }

        var student = await GetStudentAsync(data.StudentId, cancellationToken);
        return CreateNotification(
            envelope,
            student.Id,
            student.GuardianEmail,
            $"Alerta de incidente: {data.Severity}",
            $"Se reportó un incidente {data.Type} para {student.FullName}: " +
            data.Description);
    }

    private NotificationProcessingResult CreateNotification<TData>(
        EventEnvelope<TData> source,
        Guid studentId,
        string recipient,
        string subject,
        string body)
    {
        var now = timeProvider.GetUtcNow();
        var notification = new NotificationEntity
        {
            Id = Guid.NewGuid(),
            SourceEventId = source.EventId.ToString("D"),
            SourceEventType = source.EventType,
            StudentId = studentId,
            Channel = Channel,
            Recipient = recipient,
            Subject = subject,
            Body = body,
            Status = "Pending",
            Attempts = 0,
            CorrelationId = source.CorrelationId,
            CreatedAt = now,
            NextAttemptAt = now.Add(FirstAttemptDelay),
            SourcePayload = JsonSerializer.Serialize(source, JsonOptions)
        };
        dbContext.Notifications.Add(notification);

        logger.LogInformation(
            "Pending notification persisted. NotificationId={NotificationId} " +
            "EventId={EventId} EventType={EventType} StudentId={StudentId} " +
            "NextAttemptAt={NextAttemptAt} CorrelationId={CorrelationId} ServiceName={ServiceName}",
            notification.Id,
            source.EventId,
            source.EventType,
            studentId,
            notification.NextAttemptAt,
            source.CorrelationId,
            ServiceName);
        return new(NotificationProcessingOutcome.NotificationCreated, notification.Id);
    }

    private async Task<LocalStudent> GetStudentAsync(
        Guid studentId,
        CancellationToken cancellationToken) =>
        await dbContext.Students.SingleOrDefaultAsync(
            x => x.Id == studentId,
            cancellationToken) ?? throw new StudentReplicaNotFoundException(studentId);

    private static EventEnvelope<TData> Deserialize<TData>(
        ReadOnlyMemory<byte> body)
    {
        try
        {
            return JsonSerializer.Deserialize<EventEnvelope<TData>>(
                body.Span,
                JsonOptions) ?? throw new JsonException("Event envelope is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidNotificationEventException(
                $"Invalid {typeof(TData).Name} payload: {exception.Message}");
        }
    }

    private static void ValidateEnvelope(EventEnvelope<JsonElement> envelope)
    {
        if (envelope.EventId == Guid.Empty || envelope.Version != 1 ||
            string.IsNullOrWhiteSpace(envelope.EventType) ||
            string.IsNullOrWhiteSpace(envelope.CorrelationId) ||
            string.IsNullOrWhiteSpace(envelope.Source) ||
            envelope.EntityId == Guid.Empty || envelope.Data.ValueKind is
                JsonValueKind.Null or JsonValueKind.Undefined)
        {
            throw new InvalidNotificationEventException(
                "The event envelope does not satisfy the integration contract.");
        }
    }

    private static bool HasBlank(params string[] values) =>
        values.Any(string.IsNullOrWhiteSpace);
}
