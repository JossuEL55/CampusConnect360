using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AcademicService.Infrastructure.Migrations;

[DbContext(typeof(AcademicDbContext))]
[Migration("202607130001_InitialAcademicSchema")]
public sealed class InitialAcademicSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema("academic");
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS academic.students (id uuid PRIMARY KEY, identification varchar(20) NOT NULL UNIQUE, first_name varchar(80) NOT NULL, last_name varchar(80) NOT NULL, birth_date date NOT NULL, grade varchar(40) NOT NULL, school_id varchar(30) NOT NULL, guardian_full_name varchar(120) NOT NULL, guardian_email varchar(160) NOT NULL, guardian_phone varchar(30) NOT NULL, code varchar(20) NOT NULL UNIQUE, status varchar(30) NOT NULL, financial_status varchar(30) NOT NULL, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL);
            CREATE TABLE IF NOT EXISTS academic.enrollments (id uuid PRIMARY KEY, student_id uuid NOT NULL REFERENCES academic.students(id) ON DELETE CASCADE, school_year varchar(20) NOT NULL, grade varchar(40) NOT NULL, school_id varchar(30) NOT NULL, status varchar(20) NOT NULL, enrolled_at timestamptz NOT NULL, CONSTRAINT uk_enrollment_student_year UNIQUE(student_id, school_year));
            CREATE TABLE IF NOT EXISTS academic.academic_events (id uuid PRIMARY KEY, student_id uuid NOT NULL REFERENCES academic.students(id) ON DELETE CASCADE, event_type varchar(80) NOT NULL, source_event_id varchar(100) NOT NULL UNIQUE, correlation_id varchar(100) NOT NULL, payload text NOT NULL, occurred_at timestamptz NOT NULL);
            CREATE INDEX IF NOT EXISTS ix_academic_events_student_time ON academic.academic_events(student_id, occurred_at DESC);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("academic_events", "academic");
        migrationBuilder.DropTable("enrollments", "academic");
        migrationBuilder.DropTable("students", "academic");
    }
}
