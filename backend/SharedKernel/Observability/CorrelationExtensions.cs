using Microsoft.AspNetCore.Builder;

namespace SharedKernel.Observability;

// Registro del middleware de correlación para Gateway y microservicios.
public static class CorrelationExtensions
{
    public static WebApplication UseCorrelationId(
        this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();

        return app;
    }
}