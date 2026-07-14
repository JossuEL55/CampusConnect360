using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Persistence.Configurations;

public sealed class NotificationAttemptConfiguration : IEntityTypeConfiguration<NotificationAttempt>
{
    public void Configure(EntityTypeBuilder<NotificationAttempt> builder)
    {
        builder.ToTable("notification_attempts");
        builder.HasKey(x => x.Id).HasName("pk_notification_attempts");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.NotificationId).HasColumnName("notification_id").IsRequired();
        builder.Property(x => x.AttemptNumber).HasColumnName("attempt_number").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(30).IsRequired();
        builder.Property(x => x.ErrorMessage).HasColumnName("error_message").HasMaxLength(2000);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.HasOne(x => x.Notification).WithMany(x => x.NotificationAttempts)
            .HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_notification_attempts_notifications_notification_id");
        builder.HasIndex(x => x.NotificationId).HasDatabaseName("ix_notification_attempts_notification_id");
    }
}
