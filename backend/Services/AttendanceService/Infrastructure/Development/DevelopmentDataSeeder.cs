using AttendanceService.Domain.Entities;
using AttendanceService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AttendanceService.Infrastructure.Development;

public sealed class DevelopmentDataSeeder(AttendanceDbContext dbContext)
{
    // UUID fijos para facilitar pruebas manuales repetibles en Development.
    public static readonly Guid FirstStudentId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static readonly Guid SecondStudentId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static readonly Guid ThirdStudentId =
        Guid.Parse("33333333-3333-3333-3333-333333333333");

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var students = CreateStudents();
        var ids = students.Select(student => student.Id).ToArray();
        var enrollmentIds = students
            .Select(student => student.EnrollmentId)
            .ToArray();

        var existingStudents = await dbContext.Students
            .AsNoTracking()
            .Where(student =>
                ids.Contains(student.Id) ||
                enrollmentIds.Contains(student.EnrollmentId))
            .Select(student => new { student.Id, student.EnrollmentId })
            .ToListAsync(cancellationToken);

        var studentsToAdd = students.Where(student =>
            existingStudents.All(existing =>
                existing.Id != student.Id &&
                existing.EnrollmentId != student.EnrollmentId));

        dbContext.Students.AddRange(studentsToAdd);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static LocalStudent[] CreateStudents()
    {
        var createdAt = new DateTimeOffset(
            2026,
            7,
            1,
            12,
            0,
            0,
            TimeSpan.Zero);

        return
        [
            new LocalStudent
            {
                Id = FirstStudentId,
                StudentCode = "STU-001",
                FullName = "Ana Demo",
                Grade = "8A",
                SchoolId = "SCHOOL-DEMO",
                SchoolYear = "2026",
                GuardianEmail = "guardian.ana@example.test",
                EnrollmentId = Guid.Parse(
                    "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"),
                CreatedAt = createdAt
            },
            new LocalStudent
            {
                Id = SecondStudentId,
                StudentCode = "STU-002",
                FullName = "Bruno Demo",
                Grade = "9B",
                SchoolId = "SCHOOL-DEMO",
                SchoolYear = "2026",
                GuardianEmail = "guardian.bruno@example.test",
                EnrollmentId = Guid.Parse(
                    "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2"),
                CreatedAt = createdAt
            },
            new LocalStudent
            {
                Id = ThirdStudentId,
                StudentCode = "STU-003",
                FullName = "Carla Demo",
                Grade = "10A",
                SchoolId = "SCHOOL-DEMO",
                SchoolYear = "2026",
                GuardianEmail = "guardian.carla@example.test",
                EnrollmentId = Guid.Parse(
                    "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3"),
                CreatedAt = createdAt
            }
        ];
    }
}
