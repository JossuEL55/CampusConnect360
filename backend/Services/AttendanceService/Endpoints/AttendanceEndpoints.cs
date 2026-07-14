using AttendanceService.Application.Contracts.Requests;
using AttendanceService.Application.Contracts.Responses;
using AttendanceService.Application.Services;
using Microsoft.AspNetCore.Mvc;
using SharedKernel.Observability;

namespace AttendanceService.Endpoints;

public static class AttendanceEndpoints
{
    public static IEndpointRouteBuilder MapAttendanceEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/attendance")
            .WithTags("Attendance");

        group.MapGet("/students", GetStudentsAsync)
            .WithName("GetAttendanceStudents")
            .WithSummary("Lista los estudiantes locales de asistencia.")
            .Produces<PagedResponse<StudentListItemResponse>>()
            .ProducesValidationProblem();

        group.MapPost("/records", CreateRecordAsync)
            .WithName("CreateAttendanceRecord")
            .WithSummary("Registra una asistencia para un estudiante.")
            .Accepts<CreateAttendanceRecordRequest>("application/json")
            .Produces<AttendanceRecordResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/incidents", CreateIncidentAsync)
            .WithName("CreateAttendanceIncident")
            .WithSummary("Registra un incidente para un estudiante.")
            .Accepts<CreateIncidentRequest>("application/json")
            .Produces<IncidentResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/students/{id:guid}/history", GetStudentHistoryAsync)
            .WithName("GetAttendanceStudentHistory")
            .WithSummary("Obtiene el historial combinado del estudiante.")
            .Produces<StudentHistoryResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/incidents", GetIncidentsAsync)
            .WithName("GetAttendanceIncidents")
            .WithSummary("Lista incidentes con filtros y paginaciÃ³n.")
            .Produces<PagedResponse<IncidentResponse>>()
            .ProducesValidationProblem();

        return endpoints;
    }

    private static async Task<IResult> GetStudentsAsync(
        [AsParameters] StudentsQueryParameters query,
        IAttendanceApplicationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetStudentsAsync(
            query,
            cancellationToken);

        return ToResult(result);
    }

    private static async Task<IResult> CreateRecordAsync(
        CreateAttendanceRecordRequest request,
        HttpContext httpContext,
        IAttendanceApplicationService service,
        CancellationToken cancellationToken)
    {
        var registeredBy = httpContext.User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(registeredBy))
        {
            registeredBy = "docente";
        }

        var result = await service.CreateRecordAsync(
            request,
            registeredBy,
            GetCorrelationId(httpContext),
            cancellationToken);

        return result.IsSuccess
            ? Results.Json(result.Value, statusCode: StatusCodes.Status201Created)
            : ToErrorResult(result.Error!);
    }

    private static async Task<IResult> CreateIncidentAsync(
        CreateIncidentRequest request,
        HttpContext httpContext,
        IAttendanceApplicationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateIncidentAsync(
            request,
            GetCorrelationId(httpContext),
            cancellationToken);

        return result.IsSuccess
            ? Results.Json(result.Value, statusCode: StatusCodes.Status201Created)
            : ToErrorResult(result.Error!);
    }

    private static async Task<IResult> GetStudentHistoryAsync(
        Guid id,
        IAttendanceApplicationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetStudentHistoryAsync(
            id,
            cancellationToken);

        return ToResult(result);
    }

    private static async Task<IResult> GetIncidentsAsync(
        [AsParameters] IncidentsQueryParameters query,
        IAttendanceApplicationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetIncidentsAsync(
            query,
            cancellationToken);

        return ToResult(result);
    }

    private static IResult ToResult<T>(OperationResult<T> result) =>
        result.IsSuccess
            ? Results.Ok(result.Value)
            : ToErrorResult(result.Error!);

    private static IResult ToErrorResult(ApplicationError error)
    {
        if (error.Errors is not null)
        {
            return Results.ValidationProblem(
                error.Errors.ToDictionary(),
                detail: error.Detail,
                statusCode: error.StatusCode,
                title: error.Title);
        }

        return Results.Problem(
            detail: error.Detail,
            statusCode: error.StatusCode,
            title: error.Title);
    }

    private static string GetCorrelationId(HttpContext httpContext) =>
        httpContext.Items[CorrelationConstants.LogPropertyName]?.ToString()
        ?? throw new InvalidOperationException(
            "The correlation identifier was not initialized.");
}
