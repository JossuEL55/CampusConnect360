using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace SharedKernel.Observability;

// Obtiene o genera un identificador de correlación para cada solicitud HTTP,
// lo devuelve al cliente y lo incorpora a los logs estructurados.
public sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context);

        context.Items[CorrelationConstants.LogPropertyName] = correlationId;

        context.Request.Headers[CorrelationConstants.HeaderName] =
            correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationConstants.HeaderName] =
                correlationId;

            return Task.CompletedTask;
        });

        using (LogContext.PushProperty(
                   CorrelationConstants.LogPropertyName,
                   correlationId))
        {
            await _next(context);
        }
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        var receivedCorrelationId =
            context.Request.Headers[CorrelationConstants.HeaderName]
                .FirstOrDefault();

        return string.IsNullOrWhiteSpace(receivedCorrelationId)
            ? Guid.NewGuid().ToString()
            : receivedCorrelationId.Trim();
    }
}