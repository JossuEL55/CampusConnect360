using AttendanceService.Application.Contracts.Requests;
using AttendanceService.Application.Services;
using AttendanceService.Application.Validation;
using AttendanceService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;

namespace AttendanceService.Tests;

public sealed class AttendanceApplicationServiceTests
{
    [Fact]
    public async Task GetStudents_UsesPaginationDefaults_WhenOmitted()
    {
        await using var dbContext = CreateDbContext();
        var service = new AttendanceApplicationService(
            dbContext,
            TimeProvider.System);

        var result = await service.GetStudentsAsync(
            new StudentsQueryParameters(),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value?.Page);
        Assert.Equal(20, result.Value?.PageSize);
    }

    [Fact]
    public async Task GetIncidents_UsesPaginationDefaults_WhenOmitted()
    {
        await using var dbContext = CreateDbContext();
        var service = new AttendanceApplicationService(
            dbContext,
            TimeProvider.System);

        var result = await service.GetIncidentsAsync(
            new IncidentsQueryParameters(),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value?.Page);
        Assert.Equal(20, result.Value?.PageSize);
    }

    [Fact]
    public async Task CreateRecord_ReturnsNotFound_WhenStudentDoesNotExist()
    {
        await using var dbContext = CreateDbContext();
        var service = new AttendanceApplicationService(
            dbContext,
            TimeProvider.System);

        var result = await service.CreateRecordAsync(
            new CreateAttendanceRecordRequest(
                Guid.NewGuid(),
                new DateOnly(2026, 7, 15),
                AttendanceStatuses.Absent,
                "Ausencia no justificada"),
            "docente",
            "test-correlation-id",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.Error?.StatusCode);
        Assert.Empty(dbContext.AttendanceRecords);
    }

    [Fact]
    public async Task GetHistory_ReturnsNotFound_WhenStudentDoesNotExist()
    {
        await using var dbContext = CreateDbContext();
        var service = new AttendanceApplicationService(
            dbContext,
            TimeProvider.System);

        var result = await service.GetStudentHistoryAsync(
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.Error?.StatusCode);
    }

    private static AttendanceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AttendanceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AttendanceDbContext(options);
    }
}
