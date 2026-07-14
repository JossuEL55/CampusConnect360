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
        group.MapGet("/dlq", GetFailedMessagesAsync)
            .Produces<PagedResponse<FailedMessageResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);
        group.MapPost("/{id:guid}/retry", RetryNotificationAsync)
            .Produces<NotificationRetryResponse>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        group.MapPost("/demo/failure", SetFailureMode)
            .Accepts<FailureModeRequest>("application/json")
            .Produces<FailureModeResponse>();
        return endpoints;
    }

    private static async Task<IResult> GetFailedMessagesAsync(
        [AsParameters] FailedMessageQueryParameters parameters,
        NotificationQueryService service, HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.GetFailedMessagesAsync(parameters, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : Problem(context,
            "Filtros inválidos", "Uno o más filtros no son válidos.",
            result.StatusCode, result.Errors);
    }

    private static async Task<IResult> RetryNotificationAsync(
        Guid id, NotificationRetryService service, HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.RetryAsync(id, cancellationToken);
        return result.Outcome switch
        {
            NotificationRetryOutcome.Accepted => Results.Accepted(
                $"/api/notifications/{id}",
                new NotificationRetryResponse(id, "Pending", result.NextAttemptAt!.Value)),
            NotificationRetryOutcome.NotFound => Problem(context,
                "Notificación no encontrada", $"No existe la notificación con id {id}.", 404),
            NotificationRetryOutcome.FailureModeEnabled => Problem(context,
                "Modo de falla activo", "Desactive el modo de falla antes de reintentar.", 409),
            _ => Problem(context, "Retry incompatible",
                "La notificación debe estar Failed y conservar un mensaje fallido.", 409)
        };
    }

    private static IResult SetFailureMode(
        FailureModeRequest request, NotificationFailureMode failureMode,
        TimeProvider timeProvider, ILoggerFactory loggerFactory)
    {
        failureMode.Set(request.Enabled);
        var now = timeProvider.GetUtcNow();
        loggerFactory.CreateLogger("NotificationFailureMode").LogWarning(
            "Notification simulated failure mode updated. Enabled={Enabled} UpdatedAt={UpdatedAt} ServiceName={ServiceName}",
            request.Enabled, now, "NotificationService");
        return Results.Ok(new FailureModeResponse(request.Enabled, now));
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
