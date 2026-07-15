using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaymentService.Application;
using SharedKernel.Observability;

namespace PaymentService.Api;

public sealed class GlobalExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var status = exception switch
        {
            NotFoundException => StatusCodes.Status404NotFound,
            ConflictException => StatusCodes.Status409Conflict,
            ValidationException => StatusCodes.Status400BadRequest,
            BadHttpRequestException or JsonException => StatusCodes.Status400BadRequest,
            DbUpdateException => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };
        var detail = exception switch
        {
            BadHttpRequestException or JsonException =>
                "La solicitud contiene campos vacíos, tipos de datos incorrectos o JSON inválido. " +
                "Verifica especialmente que dueDate use el formato YYYY-MM-DD y que los identificadores sean UUID válidos.",
            _ when status == StatusCodes.Status500InternalServerError =>
                "Ocurrió un error inesperado.",
            _ => exception.Message
        };

        context.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = status switch
                {
                    400 => "Validación fallida",
                    404 => "Recurso no encontrado",
                    409 => "Conflicto de negocio",
                    _ => "Error interno"
                },
                Detail = detail,
                Type = $"https://campusconnect360/errors/{status}",
                Extensions = { ["traceId"] = context.Items[CorrelationConstants.LogPropertyName]?.ToString() }
            }
        });
    }
}
