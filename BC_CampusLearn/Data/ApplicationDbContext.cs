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
    public DbSet<Admin> Admins => Set<Admin>();
    public DbSet<BcUser> BcUsers => Set<BcUser>();
    public DbSet<TutorDocument> TutorDocuments => Set<TutorDocument>();
    public DbSet<TutorApplicationSettings> TutorApplicationSettings =>
        Set<TutorApplicationSettings>();
    public DbSet<TutorApplicationReviewDecision>
        TutorApplicationReviewDecisions =>
        Set<TutorApplicationReviewDecision>();

    public DbSet<ProgrammeModule> ProgrammeModules =>
        Set<ProgrammeModule>();

    public DbSet<TutorCourseModule> TutorCourseModules =>
        Set<TutorCourseModule>();

    public DbSet<TutorModuleChangeRequest> TutorModuleChangeRequests =>
        Set<TutorModuleChangeRequest>();

    public DbSet<SeniorTutorApplication> SeniorTutorApplications =>
        Set<SeniorTutorApplication>();

    public DbSet<TutorDeregistrationRequest> TutorDeregistrationRequests =>
        Set<TutorDeregistrationRequest>();

    public DbSet<TutorAvailability> TutorAvailabilities =>
        Set<TutorAvailability>();

    public DbSet<Booking> Bookings => Set<Booking>();

    public DbSet<StudyArea> StudyAreas => Set<StudyArea>();

    public DbSet<BookingPreparationLink> BookingPreparationLinks =>
        Set<BookingPreparationLink>();

    public DbSet<MeetingLink> MeetingLinks => Set<MeetingLink>();

    public DbSet<BookingDocument> BookingDocuments =>
        Set<BookingDocument>();

    public DbSet<BookingStatusHistory> BookingStatusHistory =>
        Set<BookingStatusHistory>();

    public DbSet<SessionExecution> SessionExecutions =>
        Set<SessionExecution>();

    public DbSet<SessionMessage> SessionMessages =>
        Set<SessionMessage>();

    public DbSet<UserNotification> UserNotifications =>
        Set<UserNotification>();

    public DbSet<PlatformSettings> PlatformSettings =>
        Set<PlatformSettings>();

    public DbSet<SettingAuditLog> SettingAuditLogs =>
        Set<SettingAuditLog>();

    public DbSet<SessionReview> SessionReviews =>
        Set<SessionReview>();

    public DbSet<AdminSessionReview> AdminSessionReviews =>
        Set<AdminSessionReview>();

    public DbSet<SuperAdminSessionReview> SuperAdminSessionReviews =>
        Set<SuperAdminSessionReview>();

    public DbSet<SessionAiAssessment> SessionAiAssessments =>
        Set<SessionAiAssessment>();

    public DbSet<TutorStudentEvaluation> TutorStudentEvaluations =>
        Set<TutorStudentEvaluation>();

    public DbSet<StudentEvaluation> StudentEvaluations =>
        Set<StudentEvaluation>();

    public DbSet<ProgrammeOfStudy> ProgrammesOfStudy { get; set; }

    public DbSet<LearningResource> LearningResources =>
        Set<LearningResource>();

    public DbSet<LearningResourceDocument> LearningResourceDocuments =>
        Set<LearningResourceDocument>();

    public DbSet<ResourceSubscription> ResourceSubscriptions =>
        Set<ResourceSubscription>();

    public DbSet<ResourceComment> ResourceComments =>
        Set<ResourceComment>();

    public DbSet<ResourceTutorNomination> ResourceTutorNominations =>
        Set<ResourceTutorNomination>();

    public DbSet<CampusEvent> CampusEvents => Set<CampusEvent>();

    public DbSet<CampusEventDetail> CampusEventDetails =>
        Set<CampusEventDetail>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        SynchronizeTutorRoles();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        await SynchronizeTutorRolesAsync(cancellationToken);
        return await base.SaveChangesAsync(
            acceptAllChangesOnSuccess,
            cancellationToken);
    }

    private void SynchronizeTutorRoles()
    {
        ChangeTracker.DetectChanges();
        foreach (var tutorEntry in ChangedTutorEntries())
        {
            BcUser? user = tutorEntry.Entity.BcUser ??
                BcUsers.Local.FirstOrDefault(item =>
                    item.BcUserId == tutorEntry.Entity.BcUserId) ??
                BcUsers.Find(tutorEntry.Entity.BcUserId);
            ApplyTutorRole(user, tutorEntry);
        }
    }

    private async Task SynchronizeTutorRolesAsync(
        CancellationToken cancellationToken)
    {
        ChangeTracker.DetectChanges();
        foreach (var tutorEntry in ChangedTutorEntries())
        {
            BcUser? user = tutorEntry.Entity.BcUser ??
                BcUsers.Local.FirstOrDefault(item =>
                    item.BcUserId == tutorEntry.Entity.BcUserId) ??
                await BcUsers.FindAsync(
                    [tutorEntry.Entity.BcUserId],
                    cancellationToken);
            ApplyTutorRole(user, tutorEntry);
        }
    }

    private IReadOnlyList<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<Tutor>>
        ChangedTutorEntries() => ChangeTracker.Entries<Tutor>()
            .Where(entry => entry.State is EntityState.Added or
                EntityState.Modified or EntityState.Deleted)
            .ToList();

    private static void ApplyTutorRole(
        BcUser? user,
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<Tutor> tutorEntry)
    {
        if (user is null ||
            user.Role is not (BcUserRole.Student or BcUserRole.Tutor))
        {
            return;
        }

        bool isActiveTutor = tutorEntry.State != EntityState.Deleted &&
            tutorEntry.Entity.Status == TutorStatus.Approved &&
            tutorEntry.Entity.ApplicationStage == TutorApplicationStage.Placement &&
            tutorEntry.Entity.IsActive;
        user.Role = isActiveTutor ? BcUserRole.Tutor : BcUserRole.Student;
    }


    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ApplicationDbContext).Assembly);
    }
}
