using AnalyticsService.Endpoints;

namespace AnalyticsService.Tests;

public class PaginationTests
{
    [Theory]
    [InlineData(null, null, 1, 20)]
    [InlineData(3, 50, 3, 50)]
    [InlineData(0, 0, 1, 20)]
    [InlineData(-5, 500, 1, 20)]
    public void NormalizaPaginaYTamano(int? page, int? pageSize, int expectedPage, int expectedPageSize)
    {
        var (currentPage, currentPageSize) = Pagination.Normalize(page, pageSize);

        Assert.Equal(expectedPage, currentPage);
        Assert.Equal(expectedPageSize, currentPageSize);
    }
}
