using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Persistence.Configurations;

public sealed class FailedMessageConfiguration : IEntityTypeConfiguration<FailedMessage>
{
    public void Configure(EntityTypeBuilder<FailedMessage> builder)
    {
        builder.ToTable("failed_messages");
        builder.HasKey(x => x.Id).HasName("pk_failed_messages");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.NotificationId).HasColumnName("notification_id").IsRequired();
        builder.Property(x => x.SourceEventId).HasColumnName("source_event_id").HasMaxLength(150).IsRequired();
        builder.Property(x => x.SourceEventType).HasColumnName("source_event_type").HasMaxLength(100).IsRequired();
        builder.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(100).IsRequired();
        builder.Property(x => x.OriginalPayload).HasColumnName("original_payload").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.FailureReason).HasColumnName("failure_reason").HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Attempts).HasColumnName("attempts").IsRequired();
        builder.Property(x => x.FailedAt).HasColumnName("failed_at").IsRequired();
        builder.Property(x => x.RetriedAt).HasColumnName("retried_at");
        builder.Property(x => x.ResolvedAt).HasColumnName("resolved_at");
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(30).IsRequired();
        builder.Property(x => x.DeadLetterPublishedAt).HasColumnName("dead_letter_published_at");
        builder.Property(x => x.DeadLetterAttempts).HasColumnName("dead_letter_attempts").HasDefaultValue(0).IsRequired();
        builder.Property(x => x.DeadLetterLastError).HasColumnName("dead_letter_last_error").HasMaxLength(2000);
        builder.HasOne(x => x.Notification).WithMany(x => x.FailedMessages)
            .HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_failed_messages_notifications_notification_id");
        builder.HasIndex(x => x.NotificationId).HasDatabaseName("ix_failed_messages_notification_id");
        builder.HasIndex(x => x.Status).HasDatabaseName("ix_failed_messages_status");
        builder.HasIndex(x => x.FailedAt).HasDatabaseName("ix_failed_messages_failed_at");
        builder.HasIndex(x => x.DeadLetterPublishedAt).HasDatabaseName("ix_failed_messages_dead_letter_published_at");
    }
}
