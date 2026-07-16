namespace SharedKernel.Events;

// Catálogo centralizado de eventos de negocio del ecosistema.
public static class EventTypes
{
    public const string StudentEnrolled = nameof(StudentEnrolled);

    public const string PaymentConfirmed = nameof(PaymentConfirmed);

    public const string AttendanceRecorded = nameof(AttendanceRecorded);

    public const string IncidentReported = nameof(IncidentReported);

    public const string NotificationSent = nameof(NotificationSent);

    public const string NotificationFailed = nameof(NotificationFailed);

    public const string StudentStatusUpdated = nameof(StudentStatusUpdated);
}