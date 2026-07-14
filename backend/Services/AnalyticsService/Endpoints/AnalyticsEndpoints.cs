using System.Text.Json;
using AnalyticsService.Monitoring;
using Npgsql;
using NpgsqlTypes;

namespace AnalyticsService.Endpoints;

// Endpoints de lectura del contrato, sección 9.1. La autorización por rol la aplica el Gateway.
public static class AnalyticsEndpoints
{
    public static void MapAnalyticsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/analytics");

        group.MapGet("/dashboard", GetDashboardAsync);
        group.MapGet("/events", GetEventsAsync);
        group.MapGet("/events/{correlationId}/trace", GetTraceAsync);
        group.MapGet("/failures", GetFailuresAsync);
        group.MapGet("/ecosystem-status", GetEcosystemStatusAsync);
    }

    private static async Task<IResult> GetDashboardAsync(
        NpgsqlDataSource dataSource, EcosystemMonitor monitor, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        long enrolledTotal = 0, confirmedTotal = 0, recordsTotal = 0, incidentsTotal = 0, highSeverity = 0,
            sentTotal = 0, failedTotal = 0;
        decimal confirmedAmount = 0;

        await using (var summary = new NpgsqlCommand("SELECT * FROM dashboard_summary WHERE id = 1", connection))
        await using (var reader = await summary.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                enrolledTotal = reader.GetInt64(reader.GetOrdinal("students_enrolled_total"));
                confirmedTotal = reader.GetInt64(reader.GetOrdinal("payments_confirmed_total"));
                confirmedAmount = reader.GetDecimal(reader.GetOrdinal("payments_confirmed_amount"));
                recordsTotal = reader.GetInt64(reader.GetOrdinal("attendance_records_total"));
                incidentsTotal = reader.GetInt64(reader.GetOrdinal("incidents_reported_total"));
                highSeverity = reader.GetInt64(reader.GetOrdinal("incidents_high_severity"));
                sentTotal = reader.GetInt64(reader.GetOrdinal("notifications_sent_total"));
                failedTotal = reader.GetInt64(reader.GetOrdinal("notifications_failed_total"));
            }
        }

        long enrolledToday = 0, absencesToday = 0;
        await using (var today = new NpgsqlCommand(
            "SELECT students_enrolled, absences FROM daily_metrics WHERE day = CURRENT_DATE", connection))
        await using (var reader = await today.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                enrolledToday = reader.GetInt64(0);
                absencesToday = reader.GetInt64(1);
            }
        }

        var byType = new Dictionary<string, long>();
        await using (var counts = new NpgsqlCommand(
            "SELECT event_type, count(*) FROM event_log GROUP BY event_type", connection))
        await using (var reader = await counts.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                byType[reader.GetString(0)] = reader.GetInt64(1);
            }
        }

        long failedMessages;
        await using (var failures = new NpgsqlCommand("SELECT count(*) FROM failed_messages", connection))
        {
            failedMessages = (long)(await failures.ExecuteScalarAsync(cancellationToken) ?? 0L);
        }

        var snapshot = await monitor.GetSnapshotAsync(cancellationToken);

        return Results.Ok(new
        {
            generatedAt = DateTimeOffset.UtcNow,
            students = new { enrolledTotal, enrolledToday },
            // El catálogo de eventos no incluye la creación de deudas, por lo que los
            // indicadores de pendientes permanecen en 0 hasta que Payments publique ese evento.
            payments = new { confirmedTotal, confirmedAmount, pendingTotal = 0, pendingAmount = 0m },
            attendance = new { recordsTotal, absencesToday },
            incidents = new { reportedTotal = incidentsTotal, highSeverity },
            notifications = new { sentTotal, failedTotal },
            events = new { processedTotal = byType.Values.Sum(), byType },
            failures = new { failedMessages, dlqDepth = snapshot.DlqDepth },
            ecosystemStatus = snapshot.Status
        });
    }

    private static async Task<IResult> GetEventsAsync(
        NpgsqlDataSource dataSource, string? type, string? correlationId,
        DateTimeOffset? from, DateTimeOffset? to, int? page, int? pageSize,
        CancellationToken cancellationToken)
    {
        var (currentPage, currentPageSize) = Pagination.Normalize(page, pageSize);

        const string filter = """
            WHERE (@type IS NULL OR event_type = @type)
              AND (@correlation IS NULL OR correlation_id = @correlation)
              AND (@from IS NULL OR occurred_at >= @from)
              AND (@to IS NULL OR occurred_at <= @to)
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        long totalCount;
        await using (var count = new NpgsqlCommand($"SELECT count(*) FROM event_log {filter}", connection))
        {
            AddEventFilters(count, type, correlationId, from, to);
            totalCount = (long)(await count.ExecuteScalarAsync(cancellationToken) ?? 0L);
        }

        var items = new List<object>();
        await using (var query = new NpgsqlCommand(
            $"""
            SELECT event_id, event_type, version, occurred_at, correlation_id, source, entity_id, payload::text
            FROM event_log {filter}
            ORDER BY occurred_at DESC
            LIMIT @limit OFFSET @offset
            """, connection))
        {
            AddEventFilters(query, type, correlationId, from, to);
            query.Parameters.AddWithValue("limit", currentPageSize);
            query.Parameters.AddWithValue("offset", (currentPage - 1) * currentPageSize);

            await using var reader = await query.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(ReadEventRow(reader));
            }
        }

        return Results.Ok(new { items, page = currentPage, pageSize = currentPageSize, totalCount });
    }

    private static async Task<IResult> GetTraceAsync(
        NpgsqlDataSource dataSource, string correlationId, CancellationToken cancellationToken)
    {
        var steps = new List<object>();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var query = new NpgsqlCommand(
            """
            SELECT event_id, event_type, version, occurred_at, correlation_id, source, entity_id, payload::text
            FROM event_log
            WHERE correlation_id = @correlation
            ORDER BY occurred_at
            """, connection);
        query.Parameters.AddWithValue("correlation", correlationId);

        await using var reader = await query.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            steps.Add(ReadEventRow(reader));
        }

        return steps.Count == 0
            ? Results.NotFound(new { title = "Correlación sin eventos registrados", correlationId })
            : Results.Ok(new { correlationId, totalEvents = steps.Count, steps });
    }

    private static async Task<IResult> GetFailuresAsync(
        NpgsqlDataSource dataSource, EcosystemMonitor monitor, int? page, int? pageSize,
        CancellationToken cancellationToken)
    {
        var (currentPage, currentPageSize) = Pagination.Normalize(page, pageSize);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        long totalCount;
        await using (var count = new NpgsqlCommand("SELECT count(*) FROM failed_messages", connection))
        {
            totalCount = (long)(await count.ExecuteScalarAsync(cancellationToken) ?? 0L);
        }

        var items = new List<object>();
        await using (var query = new NpgsqlCommand(
            """
            SELECT event_id, notification_id, source_event_id, source_event_type, channel,
                   recipient, attempts, failure_reason, occurred_at, correlation_id
            FROM failed_messages
            ORDER BY occurred_at DESC
            LIMIT @limit OFFSET @offset
            """, connection))
        {
            query.Parameters.AddWithValue("limit", currentPageSize);
            query.Parameters.AddWithValue("offset", (currentPage - 1) * currentPageSize);

            await using var reader = await query.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(new
                {
                    eventId = reader.GetString(0),
                    notificationId = reader.IsDBNull(1) ? null : reader.GetString(1),
                    sourceEventId = reader.IsDBNull(2) ? null : reader.GetString(2),
                    sourceEventType = reader.IsDBNull(3) ? null : reader.GetString(3),
                    channel = reader.IsDBNull(4) ? null : reader.GetString(4),
                    recipient = reader.IsDBNull(5) ? null : reader.GetString(5),
                    attempts = reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6),
                    failureReason = reader.IsDBNull(7) ? null : reader.GetString(7),
                    occurredAt = reader.GetFieldValue<DateTimeOffset>(8),
                    correlationId = reader.IsDBNull(9) ? null : reader.GetString(9)
                });
            }
        }

        var snapshot = await monitor.GetSnapshotAsync(cancellationToken);
        return Results.Ok(new { items, page = currentPage, pageSize = currentPageSize, totalCount, dlqDepth = snapshot.DlqDepth });
    }

    private static async Task<IResult> GetEcosystemStatusAsync(
        EcosystemMonitor monitor, CancellationToken cancellationToken)
    {
        var snapshot = await monitor.GetSnapshotAsync(cancellationToken);
        return Results.Ok(new
        {
            generatedAt = DateTimeOffset.UtcNow,
            ecosystemStatus = snapshot.Status,
            broker = snapshot.BrokerUp ? "Up" : "Down",
            dlqDepth = snapshot.DlqDepth,
            services = snapshot.Services.Select(service => new { name = service.Name, status = service.Status })
        });
    }

    private static void AddEventFilters(
        NpgsqlCommand command, string? type, string? correlationId, DateTimeOffset? from, DateTimeOffset? to)
    {
        command.Parameters.Add(new NpgsqlParameter("type", NpgsqlDbType.Text) { Value = (object?)type ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("correlation", NpgsqlDbType.Text) { Value = (object?)correlationId ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("from", NpgsqlDbType.TimestampTz) { Value = (object?)from ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("to", NpgsqlDbType.TimestampTz) { Value = (object?)to ?? DBNull.Value });
    }

    private static object ReadEventRow(NpgsqlDataReader reader)
    {
        using var payload = JsonDocument.Parse(reader.GetString(7));
        return new
        {
            eventId = reader.GetString(0),
            eventType = reader.GetString(1),
            version = reader.GetInt32(2),
            occurredAt = reader.GetFieldValue<DateTimeOffset>(3),
            correlationId = reader.GetString(4),
            source = reader.GetString(5),
            entityId = reader.GetString(6),
            data = payload.RootElement.Clone()
        };
    }
}
