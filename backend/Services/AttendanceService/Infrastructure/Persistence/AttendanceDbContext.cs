using AttendanceService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Configuration;

namespace AttendanceService.Infrastructure.Persistence;

public sealed class AttendanceDbContext(
    DbContextOptions<AttendanceDbContext> options) : DbContext(options)
{
    public DbSet<LocalStudent> Students => Set<LocalStudent>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DatabaseSchemas.Attendance);
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AttendanceDbContext).Assembly);
    }
}
