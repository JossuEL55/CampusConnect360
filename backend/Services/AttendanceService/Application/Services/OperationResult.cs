namespace AttendanceService.Application.Services;

public sealed record ApplicationError(
    int StatusCode,
    string Title,
    string Detail,
    IReadOnlyDictionary<string, string[]>? Errors = null);

public sealed record OperationResult<T>(T? Value, ApplicationError? Error)
{
    public bool IsSuccess => Error is null;

    public static OperationResult<T> Success(T value) =>
        new(value, null);

    public static OperationResult<T> Invalid(
        IReadOnlyDictionary<string, string[]> errors) =>
        new(
            default,
            new ApplicationError(
                StatusCodes.Status400BadRequest,
                "Solicitud invÃ¡lida",
                "Uno o mÃ¡s valores no son vÃ¡lidos.",
                errors));

    public static OperationResult<T> NotFound(string detail) =>
        new(
            default,
            new ApplicationError(
                StatusCodes.Status404NotFound,
                "Estudiante no encontrado",
                detail));
}
