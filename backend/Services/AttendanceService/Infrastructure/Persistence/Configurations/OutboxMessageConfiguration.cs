using AttendanceService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AttendanceService.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration :
    IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(message => message.Id).HasName("pk_outbox_messages");

        builder.Property(message => message.Id).HasColumnName("id");
        builder.Property(message => message.EventId)
            .HasColumnName("event_id").HasMaxLength(150).IsRequired();
        builder.Property(message => message.EventType)
            .HasColumnName("event_type").HasMaxLength(100).IsRequired();
        builder.Property(message => message.RoutingKey)
            .HasColumnName("routing_key").HasMaxLength(200).IsRequired();
        builder.Property(message => message.CorrelationId)
            .HasColumnName("correlation_id").HasMaxLength(100).IsRequired();
        builder.Property(message => message.Payload)
            .HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(message => message.OccurredAt)
            .HasColumnName("occurred_at").IsRequired();
        builder.Property(message => message.CreatedAt)
            .HasColumnName("created_at").IsRequired();
        builder.Property(message => message.ProcessedAt)
            .HasColumnName("processed_at");
        builder.Property(message => message.Attempts)
            .HasColumnName("attempts").HasDefaultValue(0).IsRequired();
        builder.Property(message => message.LastError)
            .HasColumnName("last_error").HasMaxLength(2000);

        builder.HasIndex(message => message.EventId)
            .IsUnique()
            .HasDatabaseName("ux_outbox_messages_event_id");
        builder.HasIndex(message => message.ProcessedAt)
            .HasDatabaseName("ix_outbox_messages_processed_at");
        builder.HasIndex(message => message.CreatedAt)
            .HasDatabaseName("ix_outbox_messages_created_at");
    }
}
