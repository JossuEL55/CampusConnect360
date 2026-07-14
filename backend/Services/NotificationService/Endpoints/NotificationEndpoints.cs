using Microsoft.AspNetCore.Mvc;
using NotificationService.Application.Contracts;
using NotificationService.Application.Services;
using SharedKernel.Observability;

namespace NotificationService.Endpoints;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/notifications")
            .WithTags("Notifications");
        group.MapGet("/", GetNotificationsAsync)
            .Produces<PagedResponse<NotificationListItemResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);
        group.MapGet("/{id:guid}", GetNotificationAsync)
            .Produces<NotificationDetailResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);
        return endpoints;
    }

    private static async Task<IResult> GetNotificationsAsync(
        [AsParameters] NotificationQueryParameters parameters,
        NotificationQueryService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.GetNotificationsAsync(
            parameters,
            cancellationToken);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Problem(
                context,
                "Filtros inválidos",
                "Uno o más filtros no son válidos.",
                result.StatusCode,
                result.Errors);
    }

    private static async Task<IResult> GetNotificationAsync(
        Guid id,
        NotificationQueryService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.GetNotificationAsync(id, cancellationToken);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Problem(
                context,
                "Notificación no encontrada",
                $"No existe la notificación con id {id}.",
                result.StatusCode);
    }

    private static IResult Problem(
        HttpContext context,
        string title,
        string detail,
        int status,
        IReadOnlyDictionary<string, string[]>? errors = null)
    {
        var extensions = new Dictionary<string, object?>
        {
            ["correlationId"] = context.Items[
                CorrelationConstants.LogPropertyName]?.ToString()
        };
        if (errors is not null) extensions["errors"] = errors;
        return Results.Problem(
            title: title,
            detail: detail,
            statusCode: status,
            extensions: extensions);
    }
}
