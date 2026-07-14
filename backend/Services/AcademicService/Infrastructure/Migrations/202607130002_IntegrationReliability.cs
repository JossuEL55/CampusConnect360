using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AcademicService.Infrastructure.Migrations;

[DbContext(typeof(AcademicDbContext))]
[Migration("202607130002_IntegrationReliability")]
public sealed class IntegrationReliability : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS academic.processed_events (
                event_id uuid PRIMARY KEY,
                event_type varchar(80) NOT NULL,
                processed_at timestamptz NOT NULL
            );

            CREATE TABLE IF NOT EXISTS academic.outbox_messages (
                event_id uuid PRIMARY KEY,
                event_type varchar(80) NOT NULL,
                routing_key varchar(120) NOT NULL,
                correlation_id varchar(100) NOT NULL,
                payload jsonb NOT NULL,
                occurred_at timestamptz NOT NULL,
                dispatched_at timestamptz NULL,
                attempts integer NOT NULL DEFAULT 0,
                last_error varchar(1000) NULL
            );

            CREATE INDEX IF NOT EXISTS ix_outbox_pending
                ON academic.outbox_messages(dispatched_at, occurred_at);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("outbox_messages", "academic");
        migrationBuilder.DropTable("processed_events", "academic");
    }
}
