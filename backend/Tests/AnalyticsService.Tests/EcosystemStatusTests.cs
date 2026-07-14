using AnalyticsService.Monitoring;

namespace AnalyticsService.Tests;

// Reglas del semáforo del contrato, sección 9.2.
public class EcosystemStatusTests
{
    [Theory]
    [InlineData(true, 0, 0, "Healthy")]
    [InlineData(true, 1, 0, "Degraded")]
    [InlineData(true, 0, 3, "Degraded")]
    [InlineData(true, 2, 0, "Down")]
    [InlineData(false, 0, 0, "Down")]
    public void CalculaElEstadoSegunElContrato(bool brokerUp, int unhealthyServices, int dlqDepth, string esperado)
        => Assert.Equal(esperado, EcosystemMonitor.ComputeStatus(brokerUp, unhealthyServices, dlqDepth));
}
