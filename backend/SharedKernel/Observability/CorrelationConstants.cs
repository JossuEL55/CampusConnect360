namespace SharedKernel.Observability;

// Nombres compartidos para la correlación y trazabilidad de solicitudes.
public static class CorrelationConstants
{
    public const string HeaderName = "X-Correlation-Id";

    public const string LogPropertyName = "CorrelationId";
}