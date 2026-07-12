namespace SharedKernel.Messaging;

/// Routing keys utilizadas para publicar eventos en RabbitMQ.
public static class RoutingKeys
{
    public const string StudentEnrolled =
        "academic.student.enrolled";

    public const string PaymentConfirmed =
        "payments.payment.confirmed";

    public const string AttendanceRecorded =
        "attendance.record.registered";

    public const string IncidentReported =
        "attendance.incident.reported";

    public const string NotificationSent =
        "notifications.notification.sent";

    public const string NotificationFailed =
        "notifications.notification.failed";

    public const string StudentStatusUpdated =
        "academic.student.status-updated";
}