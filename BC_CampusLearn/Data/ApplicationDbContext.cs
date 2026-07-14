using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Tutor> Tutors => Set<Tutor>();

    public DbSet<CourseModule> CourseModules =>
        Set<CourseModule>();

    public DbSet<TutorCourseModule> TutorCourseModules =>
        Set<TutorCourseModule>();

    public DbSet<TutorAvailability> TutorAvailabilities =>
        Set<TutorAvailability>();

    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ApplicationDbContext).Assembly);
    }
}