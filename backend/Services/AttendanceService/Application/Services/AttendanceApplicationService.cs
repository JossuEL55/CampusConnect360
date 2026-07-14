using AttendanceService.Application.Contracts.Requests;
using AttendanceService.Application.Contracts.Responses;
using AttendanceService.Application.Validation;
using AttendanceService.Domain.Entities;
using AttendanceService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AttendanceService.Application.Services;

public sealed class AttendanceApplicationService(
    AttendanceDbContext dbContext,
    TimeProvider timeProvider) : IAttendanceApplicationService
{
    public async Task<OperationResult<PagedResponse<StudentListItemResponse>>>
        GetStudentsAsync(
            StudentsQueryParameters query,
            CancellationToken cancellationToken)
    {
        var page = query.Page ?? 1;
        var pageSize = query.PageSize ?? PaginationRules.DefaultPageSize;
        var paginationErrors = PaginationRules.Validate(page, pageSize);

        if (paginationErrors.Count > 0)
        {
            return OperationResult<PagedResponse<StudentListItemResponse>>
                .Invalid(paginationErrors);
        }

        var students = dbContext.Students.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Grade))
        {
            var grade = query.Grade.Trim();
            students = students.Where(student => student.Grade == grade);
        }

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var pattern = $"%{query.Q.Trim()}%";
            students = students.Where(student =>
                EF.Functions.ILike(student.StudentCode, pattern) ||
                EF.Functions.ILike(student.FullName, pattern));
        }

        var totalCount = await students.CountAsync(cancellationToken);
        var items = await students
            .OrderBy(student => student.FullName)
            .ThenBy(student => student.StudentCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(student => new StudentListItemResponse(
                student.Id,
                student.StudentCode,
                student.FullName,
                student.Grade,
                student.SchoolId,
                student.SchoolYear,
                student.GuardianEmail,
                student.EnrollmentId))
            .ToListAsync(cancellationToken);

        return OperationResult<PagedResponse<StudentListItemResponse>>
            .Success(new PagedResponse<StudentListItemResponse>(
                items,
                page,
                pageSize,
                totalCount));
    }

    public async Task<OperationResult<AttendanceRecordResponse>>
        CreateRecordAsync(
            CreateAttendanceRecordRequest request,
            string registeredBy,
            string correlationId,
            CancellationToken cancellationToken)
    {
        var errors = ValidateRecord(request, registeredBy);
        if (errors.Count > 0)
        {
            return OperationResult<AttendanceRecordResponse>.Invalid(errors);
        }

        var studentExists = await dbContext.Students
            .AnyAsync(
                student => student.Id == request.StudentId,
                cancellationToken);

        if (!studentExists)
        {
            return OperationResult<AttendanceRecordResponse>.NotFound(
                $"No existe el estudiante con id {request.StudentId}.");
        }

        var record = new AttendanceRecord
        {
            Id = Guid.NewGuid(),
            StudentId = request.StudentId,
            Date = request.Date!.Value,
            Status = request.Status!,
            Remarks = request.Remarks,
            RegisteredBy = registeredBy,
            CorrelationId = correlationId,
            CreatedAt = timeProvider.GetUtcNow()
        };

        dbContext.AttendanceRecords.Add(record);
        await dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult<AttendanceRecordResponse>.Success(
            new AttendanceRecordResponse(
                record.Id,
                record.StudentId,
                record.Date,
                record.Status,
                record.Remarks,
                record.RegisteredBy,
                record.CorrelationId,
                record.CreatedAt));
    }

    public async Task<OperationResult<IncidentResponse>> CreateIncidentAsync(
        CreateIncidentRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var errors = ValidateIncident(request);
        if (errors.Count > 0)
        {
            return OperationResult<IncidentResponse>.Invalid(errors);
        }

        var studentExists = await dbContext.Students
            .AnyAsync(
                student => student.Id == request.StudentId,
                cancellationToken);

        if (!studentExists)
        {
            return OperationResult<IncidentResponse>.NotFound(
                $"No existe el estudiante con id {request.StudentId}.");
        }

        var incident = new Incident
        {
            Id = Guid.NewGuid(),
            StudentId = request.StudentId,
            Type = request.Type!,
            Severity = request.Severity!,
            Description = request.Description!.Trim(),
            ReportedBy = request.ReportedBy!.Trim(),
            CorrelationId = correlationId,
            CreatedAt = timeProvider.GetUtcNow()
        };

        dbContext.Incidents.Add(incident);
        await dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult<IncidentResponse>.Success(
            ToIncidentResponse(incident));
    }

    public async Task<OperationResult<StudentHistoryResponse>>
        GetStudentHistoryAsync(
            Guid studentId,
            CancellationToken cancellationToken)
    {
        var student = await dbContext.Students
            .AsNoTracking()
            .Where(candidate => candidate.Id == studentId)
            .Select(candidate => new StudentBasicResponse(
                candidate.Id,
                candidate.StudentCode,
                candidate.FullName,
                candidate.Grade))
            .SingleOrDefaultAsync(cancellationToken);

        if (student is null)
        {
            return OperationResult<StudentHistoryResponse>.NotFound(
                $"No existe el estudiante con id {studentId}.");
        }

        var attendanceItems = await dbContext.AttendanceRecords
            .AsNoTracking()
            .Where(record => record.StudentId == studentId)
            .Select(record => new StudentHistoryItemResponse(
                "Attendance",
                record.CreatedAt,
                record.Date,
                record.Status,
                record.Remarks,
                null,
                null,
                null))
            .ToListAsync(cancellationToken);

        var incidentItems = await dbContext.Incidents
            .AsNoTracking()
            .Where(incident => incident.StudentId == studentId)
            .Select(incident => new StudentHistoryItemResponse(
                "Incident",
                incident.CreatedAt,
                null,
                null,
                null,
                incident.Type,
                incident.Severity,
                incident.Description))
            .ToListAsync(cancellationToken);

        var items = attendanceItems
            .Concat(incidentItems)
            .OrderByDescending(item => item.OccurredAt)
            .ToList();

        return OperationResult<StudentHistoryResponse>.Success(
            new StudentHistoryResponse(student, items));
    }

    public async Task<OperationResult<PagedResponse<IncidentResponse>>>
        GetIncidentsAsync(
            IncidentsQueryParameters query,
            CancellationToken cancellationToken)
    {
        var page = query.Page ?? 1;
        var pageSize = query.PageSize ?? PaginationRules.DefaultPageSize;
        var errors = ValidateIncidentQuery(query, page, pageSize);
        if (errors.Count > 0)
        {
            return OperationResult<PagedResponse<IncidentResponse>>
                .Invalid(errors);
        }

        var incidents = dbContext.Incidents.AsNoTracking();

        if (query.StudentId.HasValue)
        {
            incidents = incidents.Where(incident =>
                incident.StudentId == query.StudentId.Value);
        }

        if (query.From.HasValue)
        {
            incidents = incidents.Where(incident =>
                incident.CreatedAt >= query.From.Value);
        }

        if (query.To.HasValue)
        {
            incidents = incidents.Where(incident =>
                incident.CreatedAt <= query.To.Value);
        }

        if (query.Type is not null)
        {
            incidents = incidents.Where(incident =>
                incident.Type == query.Type);
        }

        if (query.Severity is not null)
        {
            incidents = incidents.Where(incident =>
                incident.Severity == query.Severity);
        }

        var totalCount = await incidents.CountAsync(cancellationToken);
        var items = await incidents
            .OrderByDescending(incident => incident.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(incident => new IncidentResponse(
                incident.Id,
                incident.StudentId,
                incident.Type,
                incident.Severity,
                incident.Description,
                incident.ReportedBy,
                incident.CorrelationId,
                incident.CreatedAt))
            .ToListAsync(cancellationToken);

        return OperationResult<PagedResponse<IncidentResponse>>.Success(
            new PagedResponse<IncidentResponse>(
                items,
                page,
                pageSize,
                totalCount));
    }

    private static Dictionary<string, string[]> ValidateRecord(
        CreateAttendanceRecordRequest request,
        string registeredBy)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.StudentId == Guid.Empty)
        {
            errors["studentId"] = ["StudentId es obligatorio."];
        }

        if (!request.Date.HasValue || request.Date == default(DateOnly))
        {
            errors["date"] = ["Date es obligatoria."];
        }

        if (!AttendanceStatuses.IsValid(request.Status))
        {
            errors["status"] =
            ["Status debe ser Present, Absent, Late o Justified."];
        }

        if (request.Remarks?.Length > ValidationLimits.RemarksMaximumLength)
        {
            errors["remarks"] = ["Remarks no puede superar 1000 caracteres."];
        }

        if (string.IsNullOrWhiteSpace(registeredBy) ||
            registeredBy.Length > ValidationLimits.ReportedByMaximumLength)
        {
            errors["registeredBy"] =
            ["RegisteredBy debe tener entre 1 y 100 caracteres."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateIncident(
        CreateIncidentRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.StudentId == Guid.Empty)
        {
            errors["studentId"] = ["StudentId es obligatorio."];
        }

        if (!IncidentTypes.IsValid(request.Type))
        {
            errors["type"] =
            ["Type debe ser Academic, Disciplinary o Wellbeing."];
        }

        if (!IncidentSeverities.IsValid(request.Severity))
        {
            errors["severity"] =
            ["Severity debe ser Low, Medium o High."];
        }

        if (string.IsNullOrWhiteSpace(request.Description) ||
            request.Description.Length >
                ValidationLimits.DescriptionMaximumLength)
        {
            errors["description"] =
            ["Description es obligatoria y admite hasta 2000 caracteres."];
        }

        if (string.IsNullOrWhiteSpace(request.ReportedBy) ||
            request.ReportedBy.Length >
                ValidationLimits.ReportedByMaximumLength)
        {
            errors["reportedBy"] =
            ["ReportedBy es obligatorio y admite hasta 100 caracteres."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateIncidentQuery(
        IncidentsQueryParameters query,
        int page,
        int pageSize)
    {
        var errors = new Dictionary<string, string[]>(
            PaginationRules.Validate(page, pageSize));

        if (query.Type is not null && !IncidentTypes.IsValid(query.Type))
        {
            errors["type"] =
            ["Type debe ser Academic, Disciplinary o Wellbeing."];
        }

        if (query.Severity is not null &&
            !IncidentSeverities.IsValid(query.Severity))
        {
            errors["severity"] =
            ["Severity debe ser Low, Medium o High."];
        }

        if (query.StudentId == Guid.Empty)
        {
            errors["studentId"] = ["StudentId debe ser un UUID vÃ¡lido."];
        }

        if (query.From.HasValue && query.To.HasValue &&
            query.From.Value > query.To.Value)
        {
            errors["dateRange"] = ["From no puede ser posterior a To."];
        }

        return errors;
    }

    private static IncidentResponse ToIncidentResponse(Incident incident) =>
        new(
            incident.Id,
            incident.StudentId,
            incident.Type,
            incident.Severity,
            incident.Description,
            incident.ReportedBy,
            incident.CorrelationId,
            incident.CreatedAt);
}
