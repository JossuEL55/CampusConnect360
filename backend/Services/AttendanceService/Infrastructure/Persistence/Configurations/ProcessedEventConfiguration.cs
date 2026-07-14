using AttendanceService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AttendanceService.Infrastructure.Persistence.Configurations;

public sealed class ProcessedEventConfiguration :
    IEntityTypeConfiguration<ProcessedEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedEvent> builder)
    {
        builder.ToTable("processed_events");
        builder.HasKey(processedEvent => processedEvent.EventId)
            .HasName("pk_processed_events");

        builder.Property(processedEvent => processedEvent.EventId)
            .HasColumnName("event_id").HasMaxLength(150);
        builder.Property(processedEvent => processedEvent.EventType)
            .HasColumnName("event_type").HasMaxLength(100).IsRequired();
        builder.Property(processedEvent => processedEvent.CorrelationId)
            .HasColumnName("correlation_id").HasMaxLength(100).IsRequired();
        builder.Property(processedEvent => processedEvent.ProcessedAt)
            .HasColumnName("processed_at").IsRequired();
    }
}
