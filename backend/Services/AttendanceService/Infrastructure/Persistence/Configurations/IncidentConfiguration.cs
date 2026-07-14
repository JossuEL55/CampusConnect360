using AttendanceService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AttendanceService.Infrastructure.Persistence.Configurations;

public sealed class IncidentConfiguration : IEntityTypeConfiguration<Incident>
{
    public void Configure(EntityTypeBuilder<Incident> builder)
    {
        builder.ToTable("incidents");
        builder.HasKey(incident => incident.Id).HasName("pk_incidents");

        builder.Property(incident => incident.Id).HasColumnName("id");
        builder.Property(incident => incident.StudentId)
            .HasColumnName("student_id").IsRequired();
        builder.Property(incident => incident.Type)
            .HasColumnName("type").HasMaxLength(30).IsRequired();
        builder.Property(incident => incident.Severity)
            .HasColumnName("severity").HasMaxLength(20).IsRequired();
        builder.Property(incident => incident.Description)
            .HasColumnName("description").HasMaxLength(2000).IsRequired();
        builder.Property(incident => incident.ReportedBy)
            .HasColumnName("reported_by").HasMaxLength(100).IsRequired();
        builder.Property(incident => incident.CorrelationId)
            .HasColumnName("correlation_id").HasMaxLength(100).IsRequired();
        builder.Property(incident => incident.CreatedAt)
            .HasColumnName("created_at").IsRequired();

        builder.HasOne(incident => incident.Student)
            .WithMany(student => student.Incidents)
            .HasForeignKey(incident => incident.StudentId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_incidents_students_student_id");

        builder.HasIndex(incident => incident.StudentId)
            .HasDatabaseName("ix_incidents_student_id");
        builder.HasIndex(incident => incident.Severity)
            .HasDatabaseName("ix_incidents_severity");
        builder.HasIndex(incident => incident.CreatedAt)
            .HasDatabaseName("ix_incidents_created_at");
    }
}
