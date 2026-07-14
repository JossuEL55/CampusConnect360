using System.Text.Json;
using System.Text.Json.Serialization;
using AcademicService.Api;
using AcademicService.Domain;
using AcademicService.Infrastructure;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Events;
using SharedKernel.Messaging;

namespace AcademicService.Application;

public sealed class AcademicOperations(AcademicDbContext db)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public async Task<StudentResponse> CreateStudentAsync(StudentRequest request, CancellationToken ct)
    {
        ValidateBirthDate(request.BirthDate);
        var identification = request.Identification.Trim();
        if (await db.Students.AnyAsync(x => x.Identification == identification, ct))
            throw new ConflictException("Ya existe un estudiante con esa identificación.");

        var now = DateTimeOffset.UtcNow;
        var id = Guid.NewGuid();
        var student = new Student
        {
            Id = id,
            Identification = identification,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            BirthDate = request.BirthDate,
            Grade = request.Grade.Trim(),
            SchoolId = request.SchoolId.Trim(),
            GuardianFullName = request.Guardian.FullName.Trim(),
            GuardianEmail = request.Guardian.Email.Trim(),
            GuardianPhone = request.Guardian.Phone.Trim(),
            Code = $"STU-{id:N}"[..12].ToUpperInvariant(),
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Students.Add(student);
        await db.SaveChangesAsync(ct);
        return ToResponse(student);
    }

    public async Task<PagedResponse<StudentResponse>> ListStudentsAsync(
        string? query, int page, int pageSize, CancellationToken ct)
    {
        if (page < 1 || pageSize is < 1 or > 100)
            throw new ValidationException("page debe ser >= 1 y pageSize debe estar entre 1 y 100.");

        var students = db.Students.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = $"%{query.Trim()}%";
            students = students.Where(x =>
                EF.Functions.ILike(x.Identification, term) ||
                EF.Functions.ILike(x.Code, term) ||
                EF.Functions.ILike(x.FirstName, term) ||
                EF.Functions.ILike(x.LastName, term));
        }

        var total = await students.LongCountAsync(ct);
        var entities = await students.OrderBy(x => x.LastName).ThenBy(x => x.FirstName)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(entities.Select(ToResponse).ToList(), page, pageSize, total);
    }

    public async Task<StudentResponse> GetStudentAsync(Guid id, CancellationToken ct) =>
        ToResponse(await FindStudentAsync(id, ct));

    public async Task<StudentResponse> UpdateStudentAsync(
        Guid id, StudentRequest request, CancellationToken ct)
    {
        ValidateBirthDate(request.BirthDate);
        var student = await FindStudentAsync(id, ct);
        if (!string.Equals(student.Identification, request.Identification.Trim(), StringComparison.Ordinal))
            throw new ConflictException("La identificación no puede modificarse.");

        student.FirstName = request.FirstName.Trim();
        student.LastName = request.LastName.Trim();
        student.BirthDate = request.BirthDate;
        student.Grade = request.Grade.Trim();
        student.SchoolId = request.SchoolId.Trim();
        student.GuardianFullName = request.Guardian.FullName.Trim();
        student.GuardianEmail = request.Guardian.Email.Trim();
        student.GuardianPhone = request.Guardian.Phone.Trim();
        student.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToResponse(student);
    }

    public async Task<EnrollmentResponse> EnrollAsync(
        EnrollmentRequest request, string correlationId, CancellationToken ct)
    {
        var student = await FindStudentAsync(request.StudentId, ct);
        var schoolYear = request.SchoolYear.Trim();
        if (await db.Enrollments.AnyAsync(
                x => x.StudentId == request.StudentId && x.SchoolYear == schoolYear, ct))
            throw new ConflictException("El estudiante ya está matriculado en ese año lectivo.");

        var enrollment = new Enrollment
        {
            Id = Guid.NewGuid(),
            StudentId = request.StudentId,
            SchoolYear = schoolYear,
            Grade = request.Grade.Trim(),
            SchoolId = request.SchoolId.Trim(),
            EnrolledAt = DateTimeOffset.UtcNow
        };

        var data = new StudentEnrolledData(
            student.Id,
            student.Code,
            $"{student.FirstName} {student.LastName}",
            enrollment.Grade,
            enrollment.SchoolId,
            enrollment.SchoolYear,
            student.GuardianEmail,
            enrollment.Id);
        var envelope = EventEnvelopeFactory.Create(
            EventTypes.StudentEnrolled, "AcademicService", student.Id, correlationId, data);

        db.Enrollments.Add(enrollment);
        db.AcademicEvents.Add(ToAcademicEvent(student.Id, envelope.EventType, envelope.EventId, correlationId, envelope));
        db.OutboxMessages.Add(ToOutbox(RoutingKeys.StudentEnrolled, envelope));
        await db.SaveChangesAsync(ct);
        return ToResponse(enrollment);
    }

    public async Task<IReadOnlyList<EnrollmentResponse>> GetEnrollmentsAsync(
        Guid studentId, CancellationToken ct)
    {
        _ = await FindStudentAsync(studentId, ct);
        var entities = await db.Enrollments.AsNoTracking()
            .Where(x => x.StudentId == studentId)
            .OrderByDescending(x => x.EnrolledAt)
            .ToListAsync(ct);
        return entities.Select(ToResponse).ToList();
    }

    public async Task<IReadOnlyList<AcademicEventResponse>> GetEventsAsync(
        Guid studentId, CancellationToken ct)
    {
        _ = await FindStudentAsync(studentId, ct);
        return await db.AcademicEvents.AsNoTracking()
            .Where(x => x.StudentId == studentId)
            .OrderByDescending(x => x.OccurredAt)
            .Select(x => new AcademicEventResponse(
                x.Id, x.EventType, x.CorrelationId, x.Payload, x.OccurredAt))
            .ToListAsync(ct);
    }

    public async Task PaymentConfirmedAsync(
        EventEnvelope<PaymentConfirmedData> envelope, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var processedAt = DateTimeOffset.UtcNow;
        var inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO academic.processed_events (event_id, event_type, processed_at)
            VALUES ({envelope.EventId}, {envelope.EventType}, {processedAt})
            ON CONFLICT (event_id) DO NOTHING
            """, ct);

        if (inserted == 0)
        {
            await transaction.RollbackAsync(ct);
            return;
        }

        var studentId = envelope.Data.StudentId == Guid.Empty
            ? envelope.EntityId
            : envelope.Data.StudentId;
        var student = await FindStudentAsync(studentId, ct);
        var previousStatus = student.FinancialStatus.ToString();
        student.FinancialStatus = FinancialStatus.UpToDate;
        student.UpdatedAt = processedAt;

        db.AcademicEvents.Add(ToAcademicEvent(
            studentId, envelope.EventType, envelope.EventId, envelope.CorrelationId, envelope));

        var reasonId = envelope.Data.PaymentId == Guid.Empty
            ? envelope.EventId
            : envelope.Data.PaymentId;
        var statusData = new StudentStatusUpdatedData(
            studentId,
            previousStatus,
            FinancialStatus.UpToDate.ToString(),
            $"PaymentConfirmed:{reasonId:N}"[..25]);
        var updated = EventEnvelopeFactory.Create(
            EventTypes.StudentStatusUpdated,
            "AcademicService",
            studentId,
            envelope.CorrelationId,
            statusData);

        db.AcademicEvents.Add(ToAcademicEvent(
            studentId, updated.EventType, updated.EventId, updated.CorrelationId, updated));
        db.OutboxMessages.Add(ToOutbox(RoutingKeys.StudentStatusUpdated, updated));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task<Student> FindStudentAsync(Guid id, CancellationToken ct) =>
        await db.Students.FirstOrDefaultAsync(x => x.Id == id, ct) ??
        throw new NotFoundException("Estudiante no encontrado.");

    private static AcademicEvent ToAcademicEvent(
        Guid studentId, string type, Guid sourceId, string correlationId, object payload) => new()
    {
        Id = Guid.NewGuid(),
        StudentId = studentId,
        EventType = type,
        SourceEventId = sourceId.ToString(),
        CorrelationId = correlationId,
        Payload = JsonSerializer.Serialize(payload, JsonOptions),
        OccurredAt = DateTimeOffset.UtcNow
    };

    private static OutboxMessage ToOutbox<T>(
        string routingKey, EventEnvelope<T> envelope) => new()
    {
        EventId = envelope.EventId,
        EventType = envelope.EventType,
        RoutingKey = routingKey,
        CorrelationId = envelope.CorrelationId,
        Payload = JsonSerializer.Serialize(envelope, JsonOptions),
        OccurredAt = envelope.OccurredAt
    };

    private static StudentResponse ToResponse(Student student) => new(
        student.Id,
        student.Code,
        student.Identification,
        student.FirstName,
        student.LastName,
        student.BirthDate,
        student.Grade,
        student.SchoolId,
        new(student.GuardianFullName, student.GuardianEmail, student.GuardianPhone),
        student.Status,
        student.FinancialStatus,
        student.CreatedAt);

    private static EnrollmentResponse ToResponse(Enrollment enrollment) => new(
        enrollment.Id,
        enrollment.StudentId,
        enrollment.SchoolYear,
        enrollment.Grade,
        enrollment.SchoolId,
        enrollment.Status,
        enrollment.EnrolledAt);

    private static void ValidateBirthDate(DateOnly birthDate)
    {
        if (birthDate >= DateOnly.FromDateTime(DateTime.UtcNow))
            throw new ValidationException("La fecha de nacimiento debe estar en el pasado.");
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

public sealed class NotFoundException(string message) : Exception(message);
public sealed class ConflictException(string message) : Exception(message);
public sealed class ValidationException(string message) : Exception(message);
