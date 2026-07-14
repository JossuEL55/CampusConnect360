using System.Text.Json;
using AnalyticsService.Projections;

namespace AnalyticsService.Tests;

public class PayloadReaderTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void LeePropiedadesEnCamelCase()
    {
        var payload = Parse("""{"studentId":"stu-1","amount":350.50,"attempts":3}""");

        Assert.Equal("stu-1", PayloadReader.GetString(payload, "studentId"));
        Assert.Equal(350.50m, PayloadReader.GetDecimal(payload, "amount"));
        Assert.Equal(3, PayloadReader.GetInt(payload, "attempts"));
    }

    [Fact]
    public void LeePropiedadesEnPascalCase()
    {
        var payload = Parse("""{"StudentId":"stu-1","Amount":100,"Attempts":1}""");

        Assert.Equal("stu-1", PayloadReader.GetString(payload, "studentId"));
        Assert.Equal(100m, PayloadReader.GetDecimal(payload, "amount"));
        Assert.Equal(1, PayloadReader.GetInt(payload, "attempts"));
    }

    [Fact]
    public void PropiedadAusenteDevuelveValoresPorDefecto()
    {
        var payload = Parse("""{"otro":"valor"}""");

        Assert.Null(PayloadReader.GetString(payload, "studentId"));
        Assert.Equal(0m, PayloadReader.GetDecimal(payload, "amount"));
        Assert.Null(PayloadReader.GetInt(payload, "attempts"));
    }

    [Fact]
    public void ValorNullYPayloadNoObjetoSeTratanComoAusentes()
    {
        Assert.Null(PayloadReader.GetString(Parse("""{"remarks":null}"""), "remarks"));
        Assert.Null(PayloadReader.GetString(Parse("\"texto\""), "remarks"));
    }
}
