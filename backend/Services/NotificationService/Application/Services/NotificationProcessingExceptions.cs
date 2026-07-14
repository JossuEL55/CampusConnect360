namespace NotificationService.Application.Services;

public sealed class InvalidNotificationEventException(
    string message) : Exception(message);

public sealed class StudentReplicaNotFoundException(Guid studentId) :
    Exception($"Student {studentId} does not exist in the notification replica.")
{
    public Guid StudentId { get; } = studentId;
}
