using System.Text.Json;

namespace AnalyticsService.Projections;

// Los payloads pueden llegar en camelCase (contrato) o PascalCase (serializador .NET por defecto).
public static class PayloadReader
{
    public static bool TryGetProperty(JsonElement payload, string camelName, out JsonElement value)
    {
        value = default;
        if (payload.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        return payload.TryGetProperty(camelName, out value)
            || payload.TryGetProperty(char.ToUpperInvariant(camelName[0]) + camelName[1..], out value);
    }

    public static string? GetString(JsonElement payload, string name) =>
        TryGetProperty(payload, name, out var value) && value.ValueKind != JsonValueKind.Null ? value.ToString() : null;

    public static decimal GetDecimal(JsonElement payload, string name) =>
        TryGetProperty(payload, name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDecimal() : 0m;

    public static int? GetInt(JsonElement payload, string name) =>
        TryGetProperty(payload, name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : null;
}
