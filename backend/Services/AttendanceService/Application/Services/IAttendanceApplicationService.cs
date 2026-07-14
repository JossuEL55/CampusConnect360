using AttendanceService.Application.Contracts.Requests;
using AttendanceService.Application.Contracts.Responses;

namespace AttendanceService.Application.Services;

public interface IAttendanceApplicationService
{
    Task<OperationResult<PagedResponse<StudentListItemResponse>>>
        GetStudentsAsync(
            StudentsQueryParameters query,
            CancellationToken cancellationToken);

    Task<OperationResult<AttendanceRecordResponse>> CreateRecordAsync(
        CreateAttendanceRecordRequest request,
        string registeredBy,
        string correlationId,
        CancellationToken cancellationToken);

    Task<OperationResult<IncidentResponse>> CreateIncidentAsync(
        CreateIncidentRequest request,
        string correlationId,
        CancellationToken cancellationToken);

    Task<OperationResult<StudentHistoryResponse>> GetStudentHistoryAsync(
        Guid studentId,
        CancellationToken cancellationToken);

    Task<OperationResult<PagedResponse<IncidentResponse>>>
        GetIncidentsAsync(
            IncidentsQueryParameters query,
            CancellationToken cancellationToken);
}
