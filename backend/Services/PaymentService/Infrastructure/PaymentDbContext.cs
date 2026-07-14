using Microsoft.EntityFrameworkCore;
using PaymentService.Domain;
using SharedKernel.Configuration;

namespace PaymentService.Infrastructure;

public sealed class PaymentDbContext(DbContextOptions<PaymentDbContext> options) : DbContext(options)
{
    public DbSet<PaymentStudent> Students => Set<PaymentStudent>();
    public DbSet<Debt> Debts => Set<Debt>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentEvent> PaymentEvents => Set<PaymentEvent>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DatabaseSchemas.Payments);

        var student = modelBuilder.Entity<PaymentStudent>();
        student.ToTable("students"); student.HasKey(x => x.Id);
        student.HasIndex(x => x.StudentCode).IsUnique(); student.HasIndex(x => x.EnrollmentId).IsUnique();
        student.Property(x => x.Id).HasColumnName("id"); student.Property(x => x.StudentCode).HasColumnName("student_code").HasMaxLength(20);
        student.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(160); student.Property(x => x.Grade).HasColumnName("grade").HasMaxLength(40);
        student.Property(x => x.SchoolId).HasColumnName("school_id").HasMaxLength(30); student.Property(x => x.SchoolYear).HasColumnName("school_year").HasMaxLength(20);
        student.Property(x => x.GuardianEmail).HasColumnName("guardian_email").HasMaxLength(160); student.Property(x => x.EnrollmentId).HasColumnName("enrollment_id");
        student.Property(x => x.EnrolledAt).HasColumnName("enrolled_at"); student.Property(x => x.CreatedAt).HasColumnName("created_at"); student.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        var debt = modelBuilder.Entity<Debt>();
        debt.ToTable("debts"); debt.HasKey(x => x.Id);
        debt.HasIndex(x => new { x.StudentId, x.Status }); debt.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId);
        debt.Property(x => x.Id).HasColumnName("id"); debt.Property(x => x.StudentId).HasColumnName("student_id");
        debt.Property(x => x.Concept).HasColumnName("concept").HasMaxLength(120); debt.Property(x => x.Amount).HasColumnName("amount").HasPrecision(14, 2);
        debt.Property(x => x.DueDate).HasColumnName("due_date"); debt.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30);
        debt.Property(x => x.CreatedAt).HasColumnName("created_at"); debt.Property(x => x.PaidAt).HasColumnName("paid_at");

        var payment = modelBuilder.Entity<Payment>();
        payment.ToTable("payments"); payment.HasKey(x => x.Id);
        payment.HasIndex(x => x.DebtId).IsUnique(); payment.HasIndex(x => new { x.StudentId, x.ConfirmedAt });
        payment.HasOne(x => x.Debt).WithOne().HasForeignKey<Payment>(x => x.DebtId);
        payment.Property(x => x.Id).HasColumnName("id"); payment.Property(x => x.DebtId).HasColumnName("debt_id"); payment.Property(x => x.StudentId).HasColumnName("student_id");
        payment.Property(x => x.Amount).HasColumnName("amount").HasPrecision(14, 2); payment.Property(x => x.PaymentMethod).HasColumnName("payment_method").HasMaxLength(60);
        payment.Property(x => x.Reference).HasColumnName("reference").HasMaxLength(80); payment.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30);
        payment.Property(x => x.ConfirmedAt).HasColumnName("confirmed_at");

        var paymentEvent = modelBuilder.Entity<PaymentEvent>();
        paymentEvent.ToTable("payment_events"); paymentEvent.HasKey(x => x.Id);
        paymentEvent.HasIndex(x => x.SourceEventId).IsUnique(); paymentEvent.HasIndex(x => new { x.StudentId, x.OccurredAt });
        paymentEvent.Property(x => x.Id).HasColumnName("id"); paymentEvent.Property(x => x.StudentId).HasColumnName("student_id");
        paymentEvent.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(80); paymentEvent.Property(x => x.SourceEventId).HasColumnName("source_event_id").HasMaxLength(100);
        paymentEvent.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(100); paymentEvent.Property(x => x.Payload).HasColumnName("payload").HasColumnType("text");
        paymentEvent.Property(x => x.OccurredAt).HasColumnName("occurred_at");

        var processedEvent = modelBuilder.Entity<ProcessedEvent>();
        processedEvent.ToTable("processed_events"); processedEvent.HasKey(x => x.EventId);
        processedEvent.Property(x => x.EventId).HasColumnName("event_id"); processedEvent.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(80);
        processedEvent.Property(x => x.ProcessedAt).HasColumnName("processed_at");

        var outbox = modelBuilder.Entity<OutboxMessage>();
        outbox.ToTable("outbox_messages"); outbox.HasKey(x => x.EventId);
        outbox.HasIndex(x => new { x.DispatchedAt, x.OccurredAt }); outbox.Property(x => x.EventId).HasColumnName("event_id");
        outbox.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(80); outbox.Property(x => x.RoutingKey).HasColumnName("routing_key").HasMaxLength(120);
        outbox.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(100); outbox.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb");
        outbox.Property(x => x.OccurredAt).HasColumnName("occurred_at"); outbox.Property(x => x.DispatchedAt).HasColumnName("dispatched_at");
        outbox.Property(x => x.Attempts).HasColumnName("attempts"); outbox.Property(x => x.LastError).HasColumnName("last_error").HasMaxLength(1000);
    }
}
