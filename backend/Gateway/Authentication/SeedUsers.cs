namespace Gateway.Authentication;

// Define los usuarios de demostración y sus roles dentro del ecosistema.
public static class SeedUsers
{
    public static readonly IReadOnlyList<SeedUser> All =
    [
        new(
            "secretaria",
            "Secretaría Académica",
            "Academic",
            "Demo2026*"),

        new(
            "finanzas",
            "Área Financiera",
            "Finance",
            "Demo2026*"),

        new(
            "docente",
            "Docente / Bienestar",
            "Teacher",
            "Demo2026*"),

        new(
            "direccion",
            "Dirección General",
            "Director",
            "Demo2026*"),

        new(
            "admin",
            "Administrador",
            "Admin",
            "Demo2026*")
    ];

    public static SeedUser? Validate(
        string username,
        string password)
    {
        return All.FirstOrDefault(user =>
            string.Equals(
                user.Username,
                username,
                StringComparison.OrdinalIgnoreCase) &&
            user.Password == password);
    }
}

public sealed record SeedUser(
    string Username,
    string FullName,
    string Role,
    string Password);