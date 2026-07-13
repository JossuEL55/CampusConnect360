namespace Gateway.Authentication;

// Credenciales recibidas por el endpoint de autenticación.
public sealed record LoginRequest(
    string Username,
    string Password);

// Información pública del usuario autenticado.
public sealed record AuthenticatedUser(
    string Username,
    string FullName,
    string Role);

// Respuesta entregada después de una autenticación exitosa.
public sealed record LoginResponse(
    string AccessToken,
    int ExpiresIn,
    AuthenticatedUser User);