namespace SharedKernel.Observability;

// Centraliza los nombres utilizados para la correlación
// y trazabilidad de solicitudes dentro del ecosistema.
public static class CorrelationConstants
{
    public const string HeaderName = "X-Correlation-Id";

    public const string LogPropertyName = "CorrelationId";
}