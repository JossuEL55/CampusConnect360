namespace SharedKernel.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Secret { get; init; } = string.Empty;

    public string Issuer { get; init; } = "CampusConnect360";

    public string Audience { get; init; } = "CampusConnect360Clients";

    public int ExpirationMinutes { get; init; } = 60;
}