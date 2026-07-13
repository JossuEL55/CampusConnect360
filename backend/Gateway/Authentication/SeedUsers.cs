namespace Gateway.Authentication;

// Define los usuarios de demostración y sus roles dentro del ecosistema.
public static class SeedUsers
{
    public static readonly IReadOnlyList<SeedUser> All =
    [
        new(
            "secretaria",
            "Secretaría Académica",
            "Academic"),

        new(
            "finanzas",
            "Área Financiera",
            "Finance"),

        new(
            "docente",
            "Docente / Bienestar",
            "Teacher"),

        new(
            "direccion",
            "Dirección General",
            "Director"),

        new(
            "admin",
            "Administrador",
            "Admin")
    ];

    public static SeedUser? Validate(
        string username,
        string password,
        string configuredPassword)
    {
        if (string.IsNullOrWhiteSpace(configuredPassword) ||
            password != configuredPassword)
        {
            return null;
        }

        return All.FirstOrDefault(user =>
            string.Equals(
                user.Username,
                username,
                StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record SeedUser(
    string Username,
    string FullName,
    string Role);