namespace SharedKernel.Messaging;

/// Nombres compartidos de exchanges y colas de RabbitMQ.
public static class RabbitMqNames
{
    public static class Exchanges
    {
        public const string Events = "campus.events";
        public const string DeadLetter = "campus.dlx";
    }

    public static class Queues
    {
        public const string PaymentsStudentEnrolled =
            "payments.student-enrolled";

        public const string AttendanceStudentEnrolled =
            "attendance.student-enrolled";

        public const string AcademicPaymentConfirmed =
            "academic.payment-confirmed";

        public const string NotificationsInbox =
            "notifications.inbox";

        public const string AnalyticsAllEvents =
            "analytics.all-events";

        public const string NotificationsDeadLetter =
            "notifications.dlq";
    }
}