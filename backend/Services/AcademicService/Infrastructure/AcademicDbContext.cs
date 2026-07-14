using AcademicService.Domain;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Configuration;

namespace AcademicService.Infrastructure;

public sealed class AcademicDbContext(DbContextOptions<AcademicDbContext> options) : DbContext(options)
{
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<AcademicEvent> AcademicEvents => Set<AcademicEvent>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DatabaseSchemas.Academic);
        var student = modelBuilder.Entity<Student>();
        student.ToTable("students"); student.HasKey(x => x.Id);
        student.HasIndex(x => x.Identification).IsUnique(); student.HasIndex(x => x.Code).IsUnique();
        student.Property(x => x.Id).HasColumnName("id"); student.Property(x => x.Identification).HasColumnName("identification").HasMaxLength(20);
        student.Property(x => x.FirstName).HasColumnName("first_name").HasMaxLength(80); student.Property(x => x.LastName).HasColumnName("last_name").HasMaxLength(80);
        student.Property(x => x.BirthDate).HasColumnName("birth_date"); student.Property(x => x.Grade).HasColumnName("grade").HasMaxLength(40);
        student.Property(x => x.SchoolId).HasColumnName("school_id").HasMaxLength(30); student.Property(x => x.GuardianFullName).HasColumnName("guardian_full_name").HasMaxLength(120);
        student.Property(x => x.GuardianEmail).HasColumnName("guardian_email").HasMaxLength(160); student.Property(x => x.GuardianPhone).HasColumnName("guardian_phone").HasMaxLength(30);
        student.Property(x => x.Code).HasColumnName("code").HasMaxLength(20); student.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30);
        student.Property(x => x.FinancialStatus).HasColumnName("financial_status").HasConversion<string>().HasMaxLength(30);
        student.Property(x => x.CreatedAt).HasColumnName("created_at"); student.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        var enrollment = modelBuilder.Entity<Enrollment>(); enrollment.ToTable("enrollments"); enrollment.HasKey(x => x.Id);
        enrollment.HasIndex(x => new { x.StudentId, x.SchoolYear }).IsUnique(); enrollment.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId);
        enrollment.Property(x => x.Id).HasColumnName("id"); enrollment.Property(x => x.StudentId).HasColumnName("student_id"); enrollment.Property(x => x.SchoolYear).HasColumnName("school_year").HasMaxLength(20);
        enrollment.Property(x => x.Grade).HasColumnName("grade").HasMaxLength(40); enrollment.Property(x => x.SchoolId).HasColumnName("school_id").HasMaxLength(30);
        enrollment.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20); enrollment.Property(x => x.EnrolledAt).HasColumnName("enrolled_at");

        var academicEvent = modelBuilder.Entity<AcademicEvent>(); academicEvent.ToTable("academic_events"); academicEvent.HasKey(x => x.Id);
        academicEvent.HasIndex(x => x.SourceEventId).IsUnique(); academicEvent.HasIndex(x => new { x.StudentId, x.OccurredAt }); academicEvent.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId);
        academicEvent.Property(x => x.Id).HasColumnName("id"); academicEvent.Property(x => x.StudentId).HasColumnName("student_id"); academicEvent.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(80);
        academicEvent.Property(x => x.SourceEventId).HasColumnName("source_event_id").HasMaxLength(100); academicEvent.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(100);
        academicEvent.Property(x => x.Payload).HasColumnName("payload").HasColumnType("text"); academicEvent.Property(x => x.OccurredAt).HasColumnName("occurred_at");

        var processedEvent = modelBuilder.Entity<ProcessedEvent>(); processedEvent.ToTable("processed_events"); processedEvent.HasKey(x => x.EventId);
        processedEvent.Property(x => x.EventId).HasColumnName("event_id"); processedEvent.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(80);
        processedEvent.Property(x => x.ProcessedAt).HasColumnName("processed_at");

        var outbox = modelBuilder.Entity<OutboxMessage>(); outbox.ToTable("outbox_messages"); outbox.HasKey(x => x.EventId);
        outbox.HasIndex(x => new { x.DispatchedAt, x.OccurredAt }); outbox.Property(x => x.EventId).HasColumnName("event_id");
        outbox.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(80); outbox.Property(x => x.RoutingKey).HasColumnName("routing_key").HasMaxLength(120);
        outbox.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(100); outbox.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb");
        outbox.Property(x => x.OccurredAt).HasColumnName("occurred_at"); outbox.Property(x => x.DispatchedAt).HasColumnName("dispatched_at");
        outbox.Property(x => x.Attempts).HasColumnName("attempts"); outbox.Property(x => x.LastError).HasColumnName("last_error").HasMaxLength(1000);
    }
}
