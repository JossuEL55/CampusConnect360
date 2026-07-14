using AttendanceService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AttendanceService.Infrastructure.Persistence.Configurations;

public sealed class AttendanceRecordConfiguration :
    IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> builder)
    {
        builder.ToTable("attendance_records");
        builder.HasKey(record => record.Id).HasName("pk_attendance_records");

        builder.Property(record => record.Id).HasColumnName("id");
        builder.Property(record => record.StudentId)
            .HasColumnName("student_id").IsRequired();
        builder.Property(record => record.Date)
            .HasColumnName("date").HasColumnType("date").IsRequired();
        builder.Property(record => record.Status)
            .HasColumnName("status").HasMaxLength(20).IsRequired();
        builder.Property(record => record.Remarks)
            .HasColumnName("remarks").HasMaxLength(1000);
        builder.Property(record => record.RegisteredBy)
            .HasColumnName("registered_by").HasMaxLength(100).IsRequired();
        builder.Property(record => record.CorrelationId)
            .HasColumnName("correlation_id").HasMaxLength(100).IsRequired();
        builder.Property(record => record.CreatedAt)
            .HasColumnName("created_at").IsRequired();

        builder.HasOne(record => record.Student)
            .WithMany(student => student.AttendanceRecords)
            .HasForeignKey(record => record.StudentId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_attendance_records_students_student_id");

        builder.HasIndex(record => new { record.StudentId, record.Date })
            .HasDatabaseName("ix_attendance_records_student_id_date");
    }
}
