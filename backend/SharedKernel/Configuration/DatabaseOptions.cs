namespace SharedKernel.Configuration;

// Contiene la configuración compartida de conexión a PostgreSQL para cada servicio.
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string Host { get; init; } = "localhost";

    public int Port { get; init; } = 5432;

    public string Name { get; init; } = "campusconnect";

    public string UserName { get; init; } = "campus_admin";

    public string Password { get; init; } = string.Empty;

    public string Schema { get; init; } = "public";

    public string ConnectionString =>
        $"Host={Host};Port={Port};Database={Name};Username={UserName};Password={Password};Search Path={Schema}";
}