using System.Security.Claims;
using System.Text;
using Gateway.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using SharedKernel.Configuration;
using SharedKernel.Observability;

// Crea y configura la aplicación Gateway.
var builder = WebApplication.CreateBuilder(args);

builder.AddCampusSerilog("Gateway");

builder.Services.AddHttpClient(
    "HealthAggregator",
    client =>
    {
        client.Timeout = TimeSpan.FromSeconds(3);
    });

// Vincula la sección "Jwt" de appsettings.json con JwtOptions.
builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection(
        JwtOptions.SectionName));

// Obtiene la configuración JWT para validar y generar tokens.
var jwtOptions =
    builder.Configuration
        .GetSection(JwtOptions.SectionName)
        .Get<JwtOptions>()
    ?? throw new InvalidOperationException(
        "No se encontró la configuración JWT.");

if (string.IsNullOrWhiteSpace(jwtOptions.Secret) ||
    jwtOptions.Secret.Length < 32)
{
    throw new InvalidOperationException(
        "La clave JWT debe tener al menos 32 caracteres.");
}

// Configura la autenticación mediante JWT Bearer.
builder.Services
    .AddAuthentication(
        JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                ValidIssuer = jwtOptions.Issuer,
                ValidAudience = jwtOptions.Audience,

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(
                            jwtOptions.Secret)),

                ClockSkew = TimeSpan.Zero
            };
    });

// Define las políticas utilizadas por las rutas de YARP.
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        "AcademicPolicy",
        policy => policy.RequireRole(
            "Academic",
            "Admin"));

    options.AddPolicy(
        "FinancePolicy",
        policy => policy.RequireRole(
            "Finance",
            "Admin"));

    options.AddPolicy(
        "TeacherPolicy",
        policy => policy.RequireRole(
            "Teacher",
            "Admin"));

    options.AddPolicy(
        "DirectorPolicy",
        policy => policy.RequireRole(
            "Director",
            "Admin"));

    // Permite el acceso a cualquier usuario con un JWT válido.
    options.AddPolicy(
        "AuthenticatedPolicy",
        policy => policy.RequireAuthenticatedUser());
});

// Lee los orígenes permitidos desde appsettings.json.
var allowedOrigins =
    builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>()
    ?? [];

if (allowedOrigins.Length == 0)
{
    throw new InvalidOperationException(
        "No se configuraron orígenes permitidos para CORS.");
}

// Configura CORS para permitir solicitudes desde los frontends.
builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "FrontendPolicy",
        policy =>
        {
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

// Servicio encargado de generar los JWT.
builder.Services.AddSingleton<JwtTokenService>();

// Carga las rutas y los clusters de YARP desde appsettings.json.
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(
        builder.Configuration.GetSection(
            "ReverseProxy"));

var app = builder.Build();

// Middleware de observabilidad.
app.UseCorrelationId();
app.UseCampusRequestLogging();

// CORS debe ejecutarse antes de autenticación y autorización.
app.UseCors("FrontendPolicy");

// La autenticación debe ejecutarse antes de la autorización.
app.UseAuthentication();
app.UseAuthorization();

// Health check propio del Gateway.
app.MapGet("/health", (HttpContext context) =>
{
    var correlationId =
        context.Items[
            CorrelationConstants.LogPropertyName
        ]?.ToString();

    return Results.Ok(new
    {
        service = "Gateway",
        status = "Healthy",
        correlationId,
        timestamp = DateTimeOffset.UtcNow
    });
})
.AllowAnonymous();

// Endpoint para iniciar sesión y generar un JWT.
app.MapPost(
    "/api/auth/login",
    (
        LoginRequest request,
        JwtTokenService tokenService,
        IConfiguration configuration) =>
    {
        if (string.IsNullOrWhiteSpace(
                request.Username) ||
            string.IsNullOrWhiteSpace(
                request.Password))
        {
            return Results.BadRequest(
                new
                {
                    title =
                        "Credenciales incompletas",
                    status = 400,
                    detail =
                        "El usuario y la contraseña son obligatorios."
                });
        }

        var demoPassword =
            configuration["Auth:DemoPassword"];

        if (string.IsNullOrWhiteSpace(demoPassword))
        {
            return Results.Problem(
                title: "Configuración de autenticación incompleta",
                detail:
                    "No se configuró la contraseña de los usuarios de demostración.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        var user = SeedUsers.Validate(
            request.Username.Trim(),
            request.Password,
            demoPassword);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        return Results.Ok(
            tokenService.CreateToken(user));
    })
    .AllowAnonymous();

// Endpoint para comprobar el usuario autenticado.
app.MapGet(
    "/api/auth/me",
    (ClaimsPrincipal principal) =>
    {
        var username =
            principal.Identity?.Name;

        var fullName =
            principal.FindFirst(
                "fullName")?.Value;

        var role =
            principal.FindFirst(
                ClaimTypes.Role)?.Value;

        return Results.Ok(new
        {
            username,
            fullName,
            role
        });
    })
    .RequireAuthorization();


app.MapGet(
    "/health/services",
    async (
        IHttpClientFactory clientFactory,
        IConfiguration configuration,
        CancellationToken cancellationToken) =>
    {
        var services =
            configuration
                .GetSection("HealthServices")
                .Get<Dictionary<string, string>>()
            ?? [];

        var client =
            clientFactory.CreateClient(
                "HealthAggregator");

        var checks = new List<object>();
        var healthyCount = 0;

        foreach (var service in services)
        {
            try
            {
                using var request =
                    new HttpRequestMessage(
                        HttpMethod.Get,
                        service.Value);

                request.Headers.TryAddWithoutValidation(
                    CorrelationConstants.HeaderName,
                    Guid.NewGuid().ToString());

                using var response =
                    await client.SendAsync(
                        request,
                        cancellationToken);

                var healthy =
                    response.IsSuccessStatusCode;

                if (healthy)
                {
                    healthyCount++;
                }

                checks.Add(new
                {
                    service = service.Key,
                    status = healthy
                        ? "Healthy"
                        : "Unhealthy",
                    statusCode =
                        (int)response.StatusCode
                });
            }
            catch (Exception exception)
            {
                checks.Add(new
                {
                    service = service.Key,
                    status = "Unreachable",
                    error = exception.Message
                });
            }
        }

        var overallStatus =
            healthyCount == services.Count
                ? "Healthy"
                : healthyCount == 0
                    ? "Down"
                    : "Degraded";

        return Results.Ok(new
        {
            ecosystem = "CampusConnect360",
            status = overallStatus,
            checkedAt = DateTimeOffset.UtcNow,
            healthyServices = healthyCount,
            totalServices = services.Count,
            services = checks
        });
    })
    .AllowAnonymous();
// Publica las rutas configuradas en ReverseProxy.
app.MapReverseProxy();

app.Run();
 
