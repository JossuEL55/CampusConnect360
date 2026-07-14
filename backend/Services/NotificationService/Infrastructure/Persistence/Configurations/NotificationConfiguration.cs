using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");
        builder.HasKey(x => x.Id).HasName("pk_notifications");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SourceEventId).HasColumnName("source_event_id").HasMaxLength(150).IsRequired();
        builder.Property(x => x.SourceEventType).HasColumnName("source_event_type").HasMaxLength(100).IsRequired();
        builder.Property(x => x.StudentId).HasColumnName("student_id");
        builder.Property(x => x.Channel).HasColumnName("channel").HasMaxLength(30).IsRequired();
        builder.Property(x => x.Recipient).HasColumnName("recipient").HasMaxLength(254).IsRequired();
        builder.Property(x => x.Subject).HasColumnName("subject").HasMaxLength(300).IsRequired();
        builder.Property(x => x.Body).HasColumnName("body").HasMaxLength(4000).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(30).IsRequired();
        builder.Property(x => x.Attempts).HasColumnName("attempts").HasDefaultValue(1).IsRequired();
        builder.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(100).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.SentAt).HasColumnName("sent_at");
        builder.Property(x => x.FailureReason).HasColumnName("failure_reason").HasMaxLength(2000);
        builder.HasOne(x => x.Student).WithMany(x => x.Notifications).HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_notifications_students_student_id");
        builder.HasIndex(x => x.CreatedAt).HasDatabaseName("ix_notifications_created_at");
        builder.HasIndex(x => x.Status).HasDatabaseName("ix_notifications_status");
        builder.HasIndex(x => x.SourceEventType).HasDatabaseName("ix_notifications_source_event_type");
        builder.HasIndex(x => x.StudentId).HasDatabaseName("ix_notifications_student_id");
    }
}
