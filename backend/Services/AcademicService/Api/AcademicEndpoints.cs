using System.ComponentModel.DataAnnotations;
using AcademicService.Application;
using SharedKernel.Observability;

namespace AcademicService.Api;

public static class AcademicEndpoints
{
    public static IEndpointRouteBuilder MapAcademicEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/academic");
        group.MapPost("/students", CreateStudent)
            .Produces<StudentResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);
        group.MapGet("/students", ListStudents)
            .ProducesProblem(StatusCodes.Status400BadRequest);
        group.MapGet("/students/{id:guid}", (Guid id, AcademicOperations service, CancellationToken ct) => service.GetStudentAsync(id, ct))
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapPut("/students/{id:guid}", UpdateStudent)
            .Produces<StudentResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        group.MapPost("/enrollments", Enroll)
            .Produces<EnrollmentResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        group.MapGet("/students/{id:guid}/enrollments", (Guid id, AcademicOperations service, CancellationToken ct) => service.GetEnrollmentsAsync(id, ct))
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapGet("/students/{id:guid}/events", (Guid id, AcademicOperations service, CancellationToken ct) => service.GetEventsAsync(id, ct))
            .ProducesProblem(StatusCodes.Status404NotFound);
        return endpoints;
    }

    private static async Task<IResult> CreateStudent(StudentRequest request, AcademicOperations service, CancellationToken ct)
    {
        Validate(request); var response = await service.CreateStudentAsync(request, ct); return Results.Created($"/api/academic/students/{response.StudentId}", response);
    }
    private static Task<PagedResponse<StudentResponse>> ListStudents(string? q, int page = 1, int pageSize = 20, AcademicOperations? service = null, CancellationToken ct = default) => service!.ListStudentsAsync(q, page, pageSize, ct);
    private static async Task<StudentResponse> UpdateStudent(Guid id, StudentRequest request, AcademicOperations service, CancellationToken ct) { Validate(request); return await service.UpdateStudentAsync(id, request, ct); }
    private static async Task<IResult> Enroll(EnrollmentRequest request, HttpContext context, AcademicOperations service, CancellationToken ct)
    {
        Validate(request); var correlationId = context.Items[CorrelationConstants.LogPropertyName]?.ToString() ?? Guid.NewGuid().ToString(); var response = await service.EnrollAsync(request, correlationId, ct); return Results.Created($"/api/academic/enrollments/{response.EnrollmentId}", response);
    }
    private static void Validate(object model)
    {
        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(model, new ValidationContext(model), results, true))
            throw new Application.ValidationException(
                results[0].ErrorMessage ?? "La solicitud contiene campos inválidos.");

        if (model is StudentRequest student)
        {
            if (student.BirthDate == default)
                throw new Application.ValidationException(
                    "birthDate es obligatorio y debe tener formato YYYY-MM-DD.");

            if (student.Guardian is null)
                throw new Application.ValidationException("guardian es obligatorio.");

            Validate(student.Guardian);
        }

        if (model is EnrollmentRequest enrollment && enrollment.StudentId == Guid.Empty)
            throw new Application.ValidationException("studentId es obligatorio y debe ser un UUID válido.");
    }
}
