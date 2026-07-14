using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotificationService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationResilience : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "attempts",
                schema: "notifications",
                table: "notifications",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 1);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_attempt_at",
                schema: "notifications",
                table: "notifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "next_attempt_at",
                schema: "notifications",
                table: "notifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_payload",
                schema: "notifications",
                table: "notifications",
                type: "jsonb",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE notifications.notifications " +
                "SET source_payload = '{}'::jsonb WHERE source_payload IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "source_payload",
                schema: "notifications",
                table: "notifications",
                type: "jsonb",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "failed_messages",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_event_id = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    source_event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    original_payload = table.Column<string>(type: "jsonb", nullable: false),
                    failure_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    failed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    retried_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    dead_letter_published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    dead_letter_attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    dead_letter_last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_failed_messages", x => x.id);
                    table.ForeignKey(
                        name: "fk_failed_messages_notifications_notification_id",
                        column: x => x.notification_id,
                        principalSchema: "notifications",
                        principalTable: "notifications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_status_next_attempt_at",
                schema: "notifications",
                table: "notifications",
                columns: new[] { "status", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "ix_failed_messages_dead_letter_published_at",
                schema: "notifications",
                table: "failed_messages",
                column: "dead_letter_published_at");

            migrationBuilder.CreateIndex(
                name: "ix_failed_messages_failed_at",
                schema: "notifications",
                table: "failed_messages",
                column: "failed_at");

            migrationBuilder.CreateIndex(
                name: "ix_failed_messages_notification_id",
                schema: "notifications",
                table: "failed_messages",
                column: "notification_id");

            migrationBuilder.CreateIndex(
                name: "ix_failed_messages_status",
                schema: "notifications",
                table: "failed_messages",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "failed_messages",
                schema: "notifications");

            migrationBuilder.DropIndex(
                name: "ix_notifications_status_next_attempt_at",
                schema: "notifications",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "last_attempt_at",
                schema: "notifications",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "next_attempt_at",
                schema: "notifications",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "source_payload",
                schema: "notifications",
                table: "notifications");

            migrationBuilder.AlterColumn<int>(
                name: "attempts",
                schema: "notifications",
                table: "notifications",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);
        }
    }
}
