using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(x => x.Id).HasName("pk_outbox_messages");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.EventId).HasColumnName("event_id").HasMaxLength(150).IsRequired();
        builder.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(100).IsRequired();
        builder.Property(x => x.RoutingKey).HasColumnName("routing_key").HasMaxLength(200).IsRequired();
        builder.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.OccurredAt).HasColumnName("occurred_at").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.ProcessedAt).HasColumnName("processed_at");
        builder.Property(x => x.Attempts).HasColumnName("attempts").HasDefaultValue(0).IsRequired();
        builder.Property(x => x.LastError).HasColumnName("last_error").HasMaxLength(2000);
        builder.HasIndex(x => x.EventId).IsUnique().HasDatabaseName("ux_outbox_messages_event_id");
        builder.HasIndex(x => x.ProcessedAt).HasDatabaseName("ix_outbox_messages_processed_at");
        builder.HasIndex(x => x.CreatedAt).HasDatabaseName("ix_outbox_messages_created_at");
    }
}
