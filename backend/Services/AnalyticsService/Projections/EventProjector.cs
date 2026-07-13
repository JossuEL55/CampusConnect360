using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using SharedKernel.Events;

namespace AnalyticsService.Projections;

// Proyecta cada evento del bus en las tablas de lectura, dentro de una sola transacción.
// Patrón Idempotent Receiver: si el eventId ya está en processed_events, no se reaplica.
public sealed class EventProjector(NpgsqlDataSource dataSource, ILogger<EventProjector> logger)
{
    public async Task<bool> ApplyAsync(EventEnvelope<JsonElement> envelope, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (!await TryMarkProcessedAsync(envelope, connection, transaction, cancellationToken))
        {
            logger.LogInformation("Evento duplicado ignorado {EventId} ({EventType})", envelope.EventId, envelope.EventType);
            return false;
        }

        await InsertEventLogAsync(envelope, connection, transaction, cancellationToken);
        await ProjectByTypeAsync(envelope, connection, transaction, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static async Task<bool> TryMarkProcessedAsync(
        EventEnvelope<JsonElement> envelope, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(
            "INSERT INTO processed_events (event_id) VALUES (@id) ON CONFLICT DO NOTHING", connection, transaction);
        command.Parameters.AddWithValue("id", envelope.EventId.ToString());
        return await command.ExecuteNonQueryAsync(ct) == 1;
    }

    private static async Task InsertEventLogAsync(
        EventEnvelope<JsonElement> envelope, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO event_log (event_id, event_type, version, occurred_at, correlation_id, source, entity_id, payload)
            VALUES (@id, @type, @version, @occurred, @correlation, @source, @entity, @payload)
            ON CONFLICT DO NOTHING
            """, connection, transaction);
        command.Parameters.AddWithValue("id", envelope.EventId.ToString());
        command.Parameters.AddWithValue("type", envelope.EventType);
        command.Parameters.AddWithValue("version", envelope.Version);
        command.Parameters.AddWithValue("occurred", envelope.OccurredAt);
        command.Parameters.AddWithValue("correlation", envelope.CorrelationId);
        command.Parameters.AddWithValue("source", envelope.Source);
        command.Parameters.AddWithValue("entity", envelope.EntityId.ToString());
        command.Parameters.Add(new NpgsqlParameter("payload", NpgsqlDbType.Jsonb) { Value = envelope.Data.GetRawText() });
        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task ProjectByTypeAsync(
        EventEnvelope<JsonElement> envelope, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken ct)
    {
        var day = envelope.OccurredAt.UtcDateTime.Date;
        var data = envelope.Data;

        switch (envelope.EventType)
        {
            case EventTypes.StudentEnrolled:
                await IncrementSummaryAsync("students_enrolled_total = students_enrolled_total + 1", connection, transaction, ct);
                await IncrementDailyAsync(day, "students_enrolled = students_enrolled + 1", connection, transaction, ct);
                await UpsertStudentAsync(envelope, connection, transaction, ct);
                break;

            case EventTypes.PaymentConfirmed:
                var amount = GetDecimal(data, "amount");
                await IncrementSummaryAsync(
                    "payments_confirmed_total = payments_confirmed_total + 1, payments_confirmed_amount = payments_confirmed_amount + @amount",
                    connection, transaction, ct, new NpgsqlParameter("amount", amount));
                await IncrementDailyAsync(day,
                    "payments_confirmed = payments_confirmed + 1, payments_amount = payments_amount + @amount",
                    connection, transaction, ct, new NpgsqlParameter("amount", amount));
                await UpdateStudentStatusAsync(GetString(data, "studentId"), "UpToDate", connection, transaction, ct);
                break;

            case EventTypes.StudentStatusUpdated:
                await UpdateStudentStatusAsync(GetString(data, "studentId"), GetString(data, "newFinancialStatus"), connection, transaction, ct);
                break;

            case EventTypes.AttendanceRecorded:
                var isAbsence = GetString(data, "status") is "Absent" or "Late";
                await IncrementSummaryAsync("attendance_records_total = attendance_records_total + 1", connection, transaction, ct);
                await IncrementDailyAsync(day,
                    "attendance_records = attendance_records + 1" + (isAbsence ? ", absences = absences + 1" : string.Empty),
                    connection, transaction, ct);
                break;

            case EventTypes.IncidentReported:
                var isHigh = GetString(data, "severity") == "High";
                await IncrementSummaryAsync(
                    "incidents_reported_total = incidents_reported_total + 1" + (isHigh ? ", incidents_high_severity = incidents_high_severity + 1" : string.Empty),
                    connection, transaction, ct);
                await IncrementDailyAsync(day, "incidents = daily_metrics.incidents + 1", connection, transaction, ct);
                break;

            case EventTypes.NotificationSent:
                await IncrementSummaryAsync("notifications_sent_total = notifications_sent_total + 1", connection, transaction, ct);
                await IncrementDailyAsync(day, "notifications_sent = notifications_sent + 1", connection, transaction, ct);
                break;

            case EventTypes.NotificationFailed:
                await IncrementSummaryAsync("notifications_failed_total = notifications_failed_total + 1", connection, transaction, ct);
                await IncrementDailyAsync(day, "notifications_failed = notifications_failed + 1", connection, transaction, ct);
                await InsertFailedMessageAsync(envelope, connection, transaction, ct);
                break;

            default:
                logger.LogWarning("Evento sin proyector específico: {EventType}; solo se registró en event_log", envelope.EventType);
                break;
        }
    }

    private static async Task IncrementSummaryAsync(
        string setClause, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken ct,
        params NpgsqlParameter[] parameters)
    {
        await using var command = new NpgsqlCommand(
            $"UPDATE dashboard_summary SET {setClause} WHERE id = 1", connection, transaction);
        command.Parameters.AddRange(parameters);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task IncrementDailyAsync(
        DateTime day, string setClause, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken ct,
        params NpgsqlParameter[] parameters)
    {
        await using var ensureRow = new NpgsqlCommand(
            "INSERT INTO daily_metrics (day) VALUES (@day) ON CONFLICT (day) DO NOTHING", connection, transaction);
        ensureRow.Parameters.AddWithValue("day", DateOnly.FromDateTime(day));
        await ensureRow.ExecuteNonQueryAsync(ct);

        await using var increment = new NpgsqlCommand(
            $"UPDATE daily_metrics SET {setClause} WHERE day = @day", connection, transaction);
        increment.Parameters.AddWithValue("day", DateOnly.FromDateTime(day));
        increment.Parameters.AddRange(parameters);
        await increment.ExecuteNonQueryAsync(ct);
    }

    private static async Task UpsertStudentAsync(
        EventEnvelope<JsonElement> envelope, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO student_view (student_id, student_code, full_name, grade, school_id, enrolled_at, updated_at)
            VALUES (@id, @code, @name, @grade, @school, @enrolled, now())
            ON CONFLICT (student_id) DO UPDATE SET
                student_code = EXCLUDED.student_code,
                full_name = EXCLUDED.full_name,
                grade = EXCLUDED.grade,
                school_id = EXCLUDED.school_id,
                enrolled_at = EXCLUDED.enrolled_at,
                updated_at = now()
            """, connection, transaction);
        var data = envelope.Data;
        command.Parameters.AddWithValue("id", GetString(data, "studentId") ?? envelope.EntityId.ToString());
        command.Parameters.AddWithValue("code", (object?)GetString(data, "studentCode") ?? DBNull.Value);
        command.Parameters.AddWithValue("name", (object?)GetString(data, "fullName") ?? DBNull.Value);
        command.Parameters.AddWithValue("grade", (object?)GetString(data, "grade") ?? DBNull.Value);
        command.Parameters.AddWithValue("school", (object?)GetString(data, "schoolId") ?? DBNull.Value);
        command.Parameters.AddWithValue("enrolled", envelope.OccurredAt);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task UpdateStudentStatusAsync(
        string? studentId, string? status, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken ct)
    {
        if (studentId is null || status is null)
        {
            return;
        }

        await using var command = new NpgsqlCommand(
            "UPDATE student_view SET financial_status = @status, updated_at = now() WHERE student_id = @id", connection, transaction);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("id", studentId);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task InsertFailedMessageAsync(
        EventEnvelope<JsonElement> envelope, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO failed_messages
                (event_id, notification_id, source_event_id, source_event_type, channel, recipient, attempts, failure_reason, occurred_at, correlation_id)
            VALUES (@id, @notification, @sourceId, @sourceType, @channel, @recipient, @attempts, @reason, @occurred, @correlation)
            ON CONFLICT DO NOTHING
            """, connection, transaction);
        var data = envelope.Data;
        command.Parameters.AddWithValue("id", envelope.EventId.ToString());
        command.Parameters.AddWithValue("notification", (object?)GetString(data, "notificationId") ?? DBNull.Value);
        command.Parameters.AddWithValue("sourceId", (object?)GetString(data, "sourceEventId") ?? DBNull.Value);
        command.Parameters.AddWithValue("sourceType", (object?)GetString(data, "sourceEventType") ?? DBNull.Value);
        command.Parameters.AddWithValue("channel", (object?)GetString(data, "channel") ?? DBNull.Value);
        command.Parameters.AddWithValue("recipient", (object?)GetString(data, "recipient") ?? DBNull.Value);
        command.Parameters.AddWithValue("attempts", (object?)GetInt(data, "attempts") ?? DBNull.Value);
        command.Parameters.AddWithValue("reason", (object?)GetString(data, "failureReason") ?? DBNull.Value);
        command.Parameters.AddWithValue("occurred", envelope.OccurredAt);
        command.Parameters.AddWithValue("correlation", envelope.CorrelationId);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static string? GetString(JsonElement payload, string name) => PayloadReader.GetString(payload, name);

    private static decimal GetDecimal(JsonElement payload, string name) => PayloadReader.GetDecimal(payload, name);

    private static int? GetInt(JsonElement payload, string name) => PayloadReader.GetInt(payload, name);
}
