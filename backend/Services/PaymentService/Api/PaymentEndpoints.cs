using System.ComponentModel.DataAnnotations;
using PaymentService.Application;
using PaymentService.Domain;
using SharedKernel.Observability;

namespace PaymentService.Api;

public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/payments");

        group.MapGet("/students", (int? page, int? pageSize, PaymentOperations service, CancellationToken ct) =>
                service.ListStudentsAsync(page ?? 1, pageSize ?? 20, ct))
            .Produces<PagedResponse<StudentSummaryResponse>>();
        group.MapPost("/debts", CreateDebt)
            .Produces<DebtResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapGet("/debts", (DebtStatus? status, int? page, int? pageSize, PaymentOperations service, CancellationToken ct) =>
                service.ListDebtsAsync(status, page ?? 1, pageSize ?? 20, ct))
            .Produces<PagedResponse<DebtResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);
        group.MapPost("/debts/{debtId:guid}/confirm", ConfirmDebt)
            .Produces<PaymentResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        group.MapGet("/", (PaymentStatus? status, int? page, int? pageSize, PaymentOperations service, CancellationToken ct) =>
                service.ListPaymentsAsync(status, page ?? 1, pageSize ?? 20, ct))
            .Produces<PagedResponse<PaymentResponse>>();
        group.MapGet("/students/{studentId:guid}", (Guid studentId, PaymentOperations service, CancellationToken ct) =>
                service.GetStudentHistoryAsync(studentId, ct))
            .Produces<StudentPaymentHistoryResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapGet("/students/{studentId:guid}/events", (Guid studentId, PaymentOperations service, CancellationToken ct) =>
                service.GetEventsAsync(studentId, ct))
            .Produces<IReadOnlyList<PaymentEventResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> CreateDebt(
        DebtRequest request,
        PaymentOperations service,
        CancellationToken ct)
    {
        Validate(request);
        var response = await service.CreateDebtAsync(request, ct);
        return Results.Created($"/api/payments/debts/{response.DebtId}", response);
    }

    private static async Task<PaymentResponse> ConfirmDebt(
        Guid debtId,
        ConfirmPaymentRequest request,
        HttpContext context,
        PaymentOperations service,
        CancellationToken ct)
    {
        Validate(request);
        var correlationId = context.Items[CorrelationConstants.LogPropertyName]?.ToString() ??
                            Guid.NewGuid().ToString();
        return await service.ConfirmDebtAsync(debtId, request, correlationId, ct);
    }

    private static void Validate(object model)
    {
        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(model, new ValidationContext(model), results, true))
            throw new Application.ValidationException(
                results[0].ErrorMessage ?? "La solicitud contiene campos inválidos.");
    }
}
