using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PaymentService.Infrastructure.Migrations;

[DbContext(typeof(PaymentDbContext))]
[Migration("202607130001_InitialPaymentSchema")]
public sealed class InitialPaymentSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema("payments");
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS payments.students (
                id uuid PRIMARY KEY,
                student_code varchar(20) NOT NULL UNIQUE,
                full_name varchar(160) NOT NULL,
                grade varchar(40) NOT NULL,
                school_id varchar(30) NOT NULL,
                school_year varchar(20) NOT NULL,
                guardian_email varchar(160) NOT NULL,
                enrollment_id uuid NOT NULL UNIQUE,
                enrolled_at timestamptz NOT NULL,
                created_at timestamptz NOT NULL,
                updated_at timestamptz NOT NULL
            );

            CREATE TABLE IF NOT EXISTS payments.debts (
                id uuid PRIMARY KEY,
                student_id uuid NOT NULL REFERENCES payments.students(id) ON DELETE CASCADE,
                concept varchar(120) NOT NULL,
                amount numeric(14,2) NOT NULL,
                due_date date NOT NULL,
                status varchar(30) NOT NULL,
                created_at timestamptz NOT NULL,
                paid_at timestamptz NULL
            );

            CREATE TABLE IF NOT EXISTS payments.payments (
                id uuid PRIMARY KEY,
                debt_id uuid NOT NULL UNIQUE REFERENCES payments.debts(id) ON DELETE RESTRICT,
                student_id uuid NOT NULL REFERENCES payments.students(id) ON DELETE CASCADE,
                amount numeric(14,2) NOT NULL,
                payment_method varchar(60) NOT NULL,
                reference varchar(80) NOT NULL,
                status varchar(30) NOT NULL,
                confirmed_at timestamptz NOT NULL
            );

            CREATE TABLE IF NOT EXISTS payments.payment_events (
                id uuid PRIMARY KEY,
                student_id uuid NOT NULL REFERENCES payments.students(id) ON DELETE CASCADE,
                event_type varchar(80) NOT NULL,
                source_event_id varchar(100) NOT NULL UNIQUE,
                correlation_id varchar(100) NOT NULL,
                payload text NOT NULL,
                occurred_at timestamptz NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_payment_debts_student_status
                ON payments.debts(student_id, status);
            CREATE INDEX IF NOT EXISTS ix_payments_student_time
                ON payments.payments(student_id, confirmed_at DESC);
            CREATE INDEX IF NOT EXISTS ix_payment_events_student_time
                ON payments.payment_events(student_id, occurred_at DESC);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("payment_events", "payments");
        migrationBuilder.DropTable("payments", "payments");
        migrationBuilder.DropTable("debts", "payments");
        migrationBuilder.DropTable("students", "payments");
    }
}
