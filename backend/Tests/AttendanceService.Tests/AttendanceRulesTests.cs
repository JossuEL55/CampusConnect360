using AttendanceService.Application.Validation;

namespace AttendanceService.Tests;

public sealed class AttendanceRulesTests
{
    [Theory]
    [InlineData(AttendanceStatuses.Present)]
    [InlineData(AttendanceStatuses.Absent)]
    [InlineData(AttendanceStatuses.Late)]
    [InlineData(AttendanceStatuses.Justified)]
    public void AttendanceStatus_AcceptsDocumentedValues(string status)
    {
        Assert.True(AttendanceStatuses.IsValid(status));
    }

    [Theory]
    [InlineData("present")]
    [InlineData("Unknown")]
    [InlineData("")]
    [InlineData(null)]
    public void AttendanceStatus_RejectsOtherValues(string? status)
    {
        Assert.False(AttendanceStatuses.IsValid(status));
    }

    [Theory]
    [InlineData(IncidentTypes.Academic)]
    [InlineData(IncidentTypes.Disciplinary)]
    [InlineData(IncidentTypes.Wellbeing)]
    public void IncidentType_AcceptsDocumentedValues(string type)
    {
        Assert.True(IncidentTypes.IsValid(type));
    }

    [Theory]
    [InlineData("academic")]
    [InlineData("Other")]
    [InlineData("")]
    [InlineData(null)]
    public void IncidentType_RejectsOtherValues(string? type)
    {
        Assert.False(IncidentTypes.IsValid(type));
    }

    [Theory]
    [InlineData(IncidentSeverities.Low)]
    [InlineData(IncidentSeverities.Medium)]
    [InlineData(IncidentSeverities.High)]
    public void IncidentSeverity_AcceptsDocumentedValues(string severity)
    {
        Assert.True(IncidentSeverities.IsValid(severity));
    }

    [Theory]
    [InlineData("high")]
    [InlineData("Critical")]
    [InlineData("")]
    [InlineData(null)]
    public void IncidentSeverity_RejectsOtherValues(string? severity)
    {
        Assert.False(IncidentSeverities.IsValid(severity));
    }

    [Fact]
    public void Pagination_RejectsPageBelowOne()
    {
        var errors = PaginationRules.Validate(0, 20);

        Assert.Contains("page", errors.Keys);
    }

    [Fact]
    public void Pagination_RejectsPageSizeAboveMaximum()
    {
        var errors = PaginationRules.Validate(1, 101);

        Assert.Contains("pageSize", errors.Keys);
    }
}
