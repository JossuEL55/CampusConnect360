using Npgsql;

namespace AnalyticsService.Data;

// Crea las tablas de lectura del esquema analytics si no existen (contrato, sección 14.2).
public static class AnalyticsDatabase
{
    public static async Task InitializeAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS processed_events (
                event_id     TEXT PRIMARY KEY,
                processed_at TIMESTAMPTZ NOT NULL DEFAULT now()
            );

            CREATE TABLE IF NOT EXISTS event_log (
                event_id       TEXT PRIMARY KEY,
                event_type     TEXT NOT NULL,
                version        INT NOT NULL,
                occurred_at    TIMESTAMPTZ NOT NULL,
                correlation_id TEXT NOT NULL,
                source         TEXT NOT NULL,
                entity_id      TEXT NOT NULL,
                payload        JSONB NOT NULL,
                received_at    TIMESTAMPTZ NOT NULL DEFAULT now()
            );
            CREATE INDEX IF NOT EXISTS ix_event_log_correlation ON event_log (correlation_id);
            CREATE INDEX IF NOT EXISTS ix_event_log_type_occurred ON event_log (event_type, occurred_at);

            CREATE TABLE IF NOT EXISTS dashboard_summary (
                id                         INT PRIMARY KEY,
                students_enrolled_total    BIGINT NOT NULL DEFAULT 0,
                payments_confirmed_total   BIGINT NOT NULL DEFAULT 0,
                payments_confirmed_amount  NUMERIC(14,2) NOT NULL DEFAULT 0,
                attendance_records_total   BIGINT NOT NULL DEFAULT 0,
                incidents_reported_total   BIGINT NOT NULL DEFAULT 0,
                incidents_high_severity    BIGINT NOT NULL DEFAULT 0,
                notifications_sent_total   BIGINT NOT NULL DEFAULT 0,
                notifications_failed_total BIGINT NOT NULL DEFAULT 0
            );
            INSERT INTO dashboard_summary (id) VALUES (1) ON CONFLICT DO NOTHING;

            CREATE TABLE IF NOT EXISTS student_view (
                student_id       TEXT PRIMARY KEY,
                student_code     TEXT,
                full_name        TEXT,
                grade            TEXT,
                school_id        TEXT,
                financial_status TEXT NOT NULL DEFAULT 'NoDebt',
                enrolled_at      TIMESTAMPTZ,
                updated_at       TIMESTAMPTZ NOT NULL DEFAULT now()
            );

            CREATE TABLE IF NOT EXISTS daily_metrics (
                day                  DATE PRIMARY KEY,
                students_enrolled    BIGINT NOT NULL DEFAULT 0,
                payments_confirmed   BIGINT NOT NULL DEFAULT 0,
                payments_amount      NUMERIC(14,2) NOT NULL DEFAULT 0,
                attendance_records   BIGINT NOT NULL DEFAULT 0,
                absences             BIGINT NOT NULL DEFAULT 0,
                incidents            BIGINT NOT NULL DEFAULT 0,
                notifications_sent   BIGINT NOT NULL DEFAULT 0,
                notifications_failed BIGINT NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS failed_messages (
                event_id          TEXT PRIMARY KEY,
                notification_id   TEXT,
                source_event_id   TEXT,
                source_event_type TEXT,
                channel           TEXT,
                recipient         TEXT,
                attempts          INT,
                failure_reason    TEXT,
                occurred_at       TIMESTAMPTZ NOT NULL,
                correlation_id    TEXT
            );
            """;

        await using var command = dataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
