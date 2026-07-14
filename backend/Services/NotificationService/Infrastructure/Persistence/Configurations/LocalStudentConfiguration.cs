using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Persistence.Configurations;

public sealed class LocalStudentConfiguration : IEntityTypeConfiguration<LocalStudent>
{
    public void Configure(EntityTypeBuilder<LocalStudent> builder)
    {
        builder.ToTable("students");
        builder.HasKey(x => x.Id).HasName("pk_students");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.StudentCode).HasColumnName("student_code").HasMaxLength(50).IsRequired();
        builder.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Grade).HasColumnName("grade").HasMaxLength(100).IsRequired();
        builder.Property(x => x.SchoolId).HasColumnName("school_id").HasMaxLength(100).IsRequired();
        builder.Property(x => x.SchoolYear).HasColumnName("school_year").HasMaxLength(20).IsRequired();
        builder.Property(x => x.GuardianEmail).HasColumnName("guardian_email").HasMaxLength(254).IsRequired();
        builder.Property(x => x.EnrollmentId).HasColumnName("enrollment_id").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.HasIndex(x => x.EnrollmentId).IsUnique().HasDatabaseName("ux_students_enrollment_id");
        builder.HasIndex(x => x.StudentCode).HasDatabaseName("ix_students_student_code");
    }
}
