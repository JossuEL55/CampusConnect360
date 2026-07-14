using AttendanceService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AttendanceService.Infrastructure.Persistence.Configurations;

public sealed class LocalStudentConfiguration :
    IEntityTypeConfiguration<LocalStudent>
{
    public void Configure(EntityTypeBuilder<LocalStudent> builder)
    {
        builder.ToTable("students");
        builder.HasKey(student => student.Id).HasName("pk_students");

        builder.Property(student => student.Id).HasColumnName("id");
        builder.Property(student => student.StudentCode)
            .HasColumnName("student_code").HasMaxLength(50).IsRequired();
        builder.Property(student => student.FullName)
            .HasColumnName("full_name").HasMaxLength(200).IsRequired();
        builder.Property(student => student.Grade)
            .HasColumnName("grade").HasMaxLength(100).IsRequired();
        builder.Property(student => student.SchoolId)
            .HasColumnName("school_id").HasMaxLength(100).IsRequired();
        builder.Property(student => student.SchoolYear)
            .HasColumnName("school_year").HasMaxLength(20).IsRequired();
        builder.Property(student => student.GuardianEmail)
            .HasColumnName("guardian_email").HasMaxLength(254).IsRequired();
        builder.Property(student => student.EnrollmentId)
            .HasColumnName("enrollment_id").IsRequired();
        builder.Property(student => student.CreatedAt)
            .HasColumnName("created_at").IsRequired();
        builder.Property(student => student.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasIndex(student => student.StudentCode)
            .HasDatabaseName("ix_students_student_code");
        builder.HasIndex(student => student.EnrollmentId)
            .IsUnique()
            .HasDatabaseName("ux_students_enrollment_id");
    }
}
