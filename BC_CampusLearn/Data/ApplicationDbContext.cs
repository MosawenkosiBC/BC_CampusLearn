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
    public DbSet<BcUser> BcUsers => Set<BcUser>();
    public DbSet<TutorDocument> TutorDocuments => Set<TutorDocument>();

    public DbSet<ProgrammeModule> ProgrammeModules =>
        Set<ProgrammeModule>();

    public DbSet<TutorCourseModule> TutorCourseModules =>
        Set<TutorCourseModule>();

    public DbSet<TutorModuleChangeRequest> TutorModuleChangeRequests =>
        Set<TutorModuleChangeRequest>();

    public DbSet<TutorDeregistrationRequest> TutorDeregistrationRequests =>
        Set<TutorDeregistrationRequest>();

    public DbSet<TutorAvailability> TutorAvailabilities =>
        Set<TutorAvailability>();

    public DbSet<Booking> Bookings => Set<Booking>();

    public DbSet<BookingPreparationLink> BookingPreparationLinks =>
        Set<BookingPreparationLink>();

    public DbSet<BookingDocument> BookingDocuments =>
        Set<BookingDocument>();

    public DbSet<ProgrammeOfStudy> ProgrammesOfStudy { get; set; }


    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ApplicationDbContext).Assembly);
    }
}
