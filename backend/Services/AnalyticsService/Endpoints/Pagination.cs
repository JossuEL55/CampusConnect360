namespace AnalyticsService.Endpoints;

// Normaliza los parámetros de paginación de la convención del contrato (page desde 1, pageSize 20).
public static class Pagination
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 200;

    public static (int Page, int PageSize) Normalize(int? page, int? pageSize) =>
        (page is null or < 1 ? 1 : page.Value,
         pageSize is null or < 1 or > MaxPageSize ? DefaultPageSize : pageSize.Value);
}
