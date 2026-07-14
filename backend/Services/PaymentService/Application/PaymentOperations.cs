using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using PaymentService.Api;
using PaymentService.Domain;
using PaymentService.Infrastructure;
using SharedKernel.Events;
using SharedKernel.Messaging;

namespace PaymentService.Application;

public sealed class PaymentOperations(PaymentDbContext db)
{
    public const decimal DefaultEnrollmentDebtAmount = 350.00m;
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public async Task<PagedResponse<StudentSummaryResponse>> ListStudentsAsync(
        int page, int pageSize, CancellationToken ct)
    {
        ValidatePage(page, pageSize);
        var query = db.Students.AsNoTracking();
        var total = await query.LongCountAsync(ct);
        var students = await query.OrderBy(x => x.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        var ids = students.Select(x => x.Id).ToList();
        var pending = await db.Debts.AsNoTracking()
            .Where(x => ids.Contains(x.StudentId) && x.Status != DebtStatus.Paid)
            .GroupBy(x => x.StudentId)
            .Select(x => new { StudentId = x.Key, Count = x.Count(), Amount = x.Sum(y => y.Amount) })
            .ToDictionaryAsync(x => x.StudentId, ct);

        return new(students.Select(x =>
        {
            pending.TryGetValue(x.Id, out var debt);
            return new StudentSummaryResponse(
                x.Id, x.StudentCode, x.FullName, x.Grade, x.SchoolId, x.SchoolYear,
                x.GuardianEmail, debt?.Amount ?? 0m, debt?.Count ?? 0, x.EnrolledAt);
        }).ToList(), page, pageSize, total);
    }

    public async Task<DebtResponse> CreateDebtAsync(DebtRequest request, CancellationToken ct)
    {
        ValidateDebtRequest(request);
        _ = await FindStudentAsync(request.StudentId, ct);
        var now = DateTimeOffset.UtcNow;
        var debt = new Debt
        {
            Id = Guid.NewGuid(),
            StudentId = request.StudentId,
            Concept = request.Concept.Trim(),
            Amount = request.Amount,
            DueDate = request.DueDate,
            CreatedAt = now
        };
        db.Debts.Add(debt);
        await db.SaveChangesAsync(ct);
        return await ToDebtResponseAsync(debt.Id, ct);
    }

    public async Task<PagedResponse<DebtResponse>> ListDebtsAsync(
        DebtStatus? status, int page, int pageSize, CancellationToken ct)
    {
        ValidatePage(page, pageSize);
        var query = db.Debts.AsNoTracking().Include(x => x.Student).AsQueryable();
        if (status is not null)
            query = query.Where(x => x.Status == status);
        var total = await query.LongCountAsync(ct);
        var debts = await query.OrderBy(x => x.DueDate).ThenBy(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return new(debts.Select(ToResponse).ToList(), page, pageSize, total);
    }

    public async Task<PaymentResponse> ConfirmDebtAsync(
        Guid debtId, ConfirmPaymentRequest request, string correlationId, CancellationToken ct)
    {
        ValidateConfirmRequest(request);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var debt = await db.Debts.Include(x => x.Student)
            .FirstOrDefaultAsync(x => x.Id == debtId, ct) ??
            throw new NotFoundException("Deuda no encontrada.");

        if (debt.Status == DebtStatus.Paid || await db.Payments.AnyAsync(x => x.DebtId == debtId, ct))
            throw new ConflictException("La deuda ya fue confirmada.");
        if (request.PaidAmount < debt.Amount)
            throw new ValidationException("paidAmount debe cubrir el total de la deuda.");

        var confirmedAt = DateTimeOffset.UtcNow;
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            DebtId = debt.Id,
            StudentId = debt.StudentId,
            Amount = debt.Amount,
            PaymentMethod = request.PaymentMethod.Trim(),
            Reference = request.Reference.Trim(),
            ConfirmedAt = confirmedAt
        };
        debt.Status = DebtStatus.Paid;
        debt.PaidAt = confirmedAt;

        var data = new PaymentConfirmedData(
            payment.Id,
            debt.Id,
            debt.StudentId,
            debt.Concept,
            debt.Amount,
            payment.PaymentMethod,
            confirmedAt);
        var envelope = EventEnvelopeFactory.Create(
            EventTypes.PaymentConfirmed,
            "PaymentService",
            debt.StudentId,
            correlationId,
            data);

        db.Payments.Add(payment);
        db.PaymentEvents.Add(ToPaymentEvent(debt.StudentId, envelope.EventType, envelope.EventId, correlationId, envelope));
        db.OutboxMessages.Add(ToOutbox(RoutingKeys.PaymentConfirmed, envelope));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToResponse(payment);
    }

    public async Task<PagedResponse<PaymentResponse>> ListPaymentsAsync(
        PaymentStatus? status, int page, int pageSize, CancellationToken ct)
    {
        ValidatePage(page, pageSize);
        var query = db.Payments.AsNoTracking().AsQueryable();
        if (status is not null)
            query = query.Where(x => x.Status == status);
        var total = await query.LongCountAsync(ct);
        var payments = await query.OrderByDescending(x => x.ConfirmedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return new(payments.Select(ToResponse).ToList(), page, pageSize, total);
    }

    public async Task<StudentPaymentHistoryResponse> GetStudentHistoryAsync(Guid studentId, CancellationToken ct)
    {
        var student = await FindStudentAsync(studentId, ct);
        var debts = await db.Debts.AsNoTracking().Include(x => x.Student)
            .Where(x => x.StudentId == studentId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
        var payments = await db.Payments.AsNoTracking()
            .Where(x => x.StudentId == studentId)
            .OrderByDescending(x => x.ConfirmedAt)
            .ToListAsync(ct);
        return new(student.Id, student.StudentCode, student.FullName,
            debts.Select(ToResponse).ToList(),
            payments.Select(ToResponse).ToList());
    }

    public async Task<IReadOnlyList<PaymentEventResponse>> GetEventsAsync(Guid studentId, CancellationToken ct)
    {
        _ = await FindStudentAsync(studentId, ct);
        return await db.PaymentEvents.AsNoTracking()
            .Where(x => x.StudentId == studentId)
            .OrderByDescending(x => x.OccurredAt)
            .Select(x => new PaymentEventResponse(x.Id, x.EventType, x.CorrelationId, x.Payload, x.OccurredAt))
            .ToListAsync(ct);
    }

    public async Task StudentEnrolledAsync(EventEnvelope<StudentEnrolledData> envelope, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var processedAt = DateTimeOffset.UtcNow;
        var inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO payments.processed_events (event_id, event_type, processed_at)
            VALUES ({envelope.EventId}, {envelope.EventType}, {processedAt})
            ON CONFLICT (event_id) DO NOTHING
            """, ct);

        if (inserted == 0)
        {
            await transaction.RollbackAsync(ct);
            return;
        }

        var student = await db.Students.FirstOrDefaultAsync(x => x.Id == envelope.Data.StudentId, ct);
        if (student is null)
        {
            student = new PaymentStudent
            {
                Id = envelope.Data.StudentId,
                StudentCode = envelope.Data.StudentCode.Trim(),
                FullName = envelope.Data.FullName.Trim(),
                Grade = envelope.Data.Grade.Trim(),
                SchoolId = envelope.Data.SchoolId.Trim(),
                SchoolYear = envelope.Data.SchoolYear.Trim(),
                GuardianEmail = envelope.Data.GuardianEmail.Trim(),
                EnrollmentId = envelope.Data.EnrollmentId,
                EnrolledAt = envelope.OccurredAt,
                CreatedAt = processedAt,
                UpdatedAt = processedAt
            };
            db.Students.Add(student);
        }
        else
        {
            student.StudentCode = envelope.Data.StudentCode.Trim();
            student.FullName = envelope.Data.FullName.Trim();
            student.Grade = envelope.Data.Grade.Trim();
            student.SchoolId = envelope.Data.SchoolId.Trim();
            student.SchoolYear = envelope.Data.SchoolYear.Trim();
            student.GuardianEmail = envelope.Data.GuardianEmail.Trim();
            student.EnrollmentId = envelope.Data.EnrollmentId;
            student.EnrolledAt = envelope.OccurredAt;
            student.UpdatedAt = processedAt;
        }

        if (!await db.Debts.AnyAsync(x =>
                x.StudentId == envelope.Data.StudentId &&
                x.Concept == $"Matrícula {envelope.Data.SchoolYear}", ct))
        {
            db.Debts.Add(new Debt
            {
                Id = Guid.NewGuid(),
                StudentId = envelope.Data.StudentId,
                Concept = $"Matrícula {envelope.Data.SchoolYear}",
                Amount = DefaultEnrollmentDebtAmount,
                DueDate = DateOnly.FromDateTime(envelope.OccurredAt.UtcDateTime.Date.AddDays(30)),
                CreatedAt = processedAt
            });
        }

        await db.SaveChangesAsync(ct);

        db.PaymentEvents.Add(ToPaymentEvent(
            envelope.Data.StudentId,
            envelope.EventType,
            envelope.EventId,
            envelope.CorrelationId,
            envelope));

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task<PaymentStudent> FindStudentAsync(Guid id, CancellationToken ct) =>
        await db.Students.FirstOrDefaultAsync(x => x.Id == id, ct) ??
        throw new NotFoundException("Estudiante no encontrado en la réplica local de pagos.");

    private async Task<DebtResponse> ToDebtResponseAsync(Guid id, CancellationToken ct)
    {
        var debt = await db.Debts.AsNoTracking().Include(x => x.Student)
            .FirstAsync(x => x.Id == id, ct);
        return ToResponse(debt);
    }

    private static PaymentEvent ToPaymentEvent(
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

    private static DebtResponse ToResponse(Debt debt) => new(
        debt.Id,
        debt.StudentId,
        debt.Student?.FullName ?? string.Empty,
        debt.Concept,
        debt.Amount,
        debt.DueDate,
        debt.Status,
        debt.CreatedAt,
        debt.PaidAt);

    private static PaymentResponse ToResponse(Payment payment) => new(
        payment.Id,
        payment.DebtId,
        payment.StudentId,
        payment.Amount,
        payment.Status,
        payment.PaymentMethod,
        payment.Reference,
        payment.ConfirmedAt);

    private static void ValidateDebtRequest(DebtRequest request)
    {
        if (request.StudentId == Guid.Empty)
            throw new ValidationException("studentId es obligatorio y debe ser un UUID válido.");
        if (request.Amount <= 0)
            throw new ValidationException("amount debe ser mayor que 0.");
        if (request.DueDate == default)
            throw new ValidationException("dueDate es obligatorio y debe tener formato YYYY-MM-DD.");
    }

    private static void ValidateConfirmRequest(ConfirmPaymentRequest request)
    {
        if (request.PaidAmount <= 0)
            throw new ValidationException("paidAmount debe ser mayor que 0.");
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100)
            throw new ValidationException("page debe ser >= 1 y pageSize debe estar entre 1 y 100.");
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
