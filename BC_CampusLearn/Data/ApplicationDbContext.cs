using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Data;

public class ApplicationDbContext : DbContext
{
    private readonly HashSet<string> _queuedRoleChanges = [];
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<NotificationTemplateSetting> NotificationTemplateSettings => Set<NotificationTemplateSetting>();

    public DbSet<Announcement> Announcements => Set<Announcement>();

    public DbSet<Tutor> Tutors => Set<Tutor>();
    public DbSet<TutorBankingDetails> TutorBankingDetails => Set<TutorBankingDetails>();
    public DbSet<TutorContract> TutorContracts => Set<TutorContract>();
    public DbSet<TutorPersonalInformation> TutorPersonalInformation =>
        Set<TutorPersonalInformation>();
    public DbSet<TutorInterviewMeeting> TutorInterviewMeetings => Set<TutorInterviewMeeting>();
    public DbSet<TutorDeregistrationEvent> TutorDeregistrationEvents => Set<TutorDeregistrationEvent>();
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

    public DbSet<SeniorTutorApplicationModule> SeniorTutorApplicationModules =>
        Set<SeniorTutorApplicationModule>();

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

    public DbSet<EmailNotificationOutbox> EmailNotificationOutbox =>
        Set<EmailNotificationOutbox>();

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
        QueueRoleChangeNotifications();
        int result = base.SaveChanges(acceptAllChangesOnSuccess);
        _queuedRoleChanges.Clear();
        return result;
    }

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        await SynchronizeTutorRolesAsync(cancellationToken);
        QueueRoleChangeNotifications();
        int result = await base.SaveChangesAsync(
            acceptAllChangesOnSuccess,
            cancellationToken);
        _queuedRoleChanges.Clear();
        return result;
    }

    private void QueueRoleChangeNotifications()
    {
        ChangeTracker.DetectChanges();
        foreach (var entry in ChangeTracker.Entries<BcUser>().Where(entry =>
            entry.State == EntityState.Modified &&
            entry.Property(user => user.Role).IsModified))
        {
            BcUserRole previous = entry.Property(user => user.Role).OriginalValue;
            BcUserRole current = entry.Entity.Role;
            string changeKey = $"{entry.Entity.BcUserId}:{previous}:{current}";
            bool demotedFromAdministrator =
                previous is BcUserRole.Admin or BcUserRole.SuperAdmin &&
                current != BcUserRole.SuperAdmin;
            if (previous == current || current == BcUserRole.Dev ||
                (current == BcUserRole.Student && !demotedFromAdministrator) ||
                !_queuedRoleChanges.Add(changeKey))
            {
                continue;
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            (string title, string message, string link) =
                demotedFromAdministrator
                ? (
                    "Administrative access updated",
                    $"Your {RoleLabel(previous)} access has been removed. " +
                    "Your account remains active, and the Mzala Connect tools available to you " +
                    "have been updated.",
                    RoleHome(current))
                : current switch
            {
                BcUserRole.Tutor => (
                    "Tutor access granted",
                    "Your account has been updated to Tutor. You can now manage your availability, bookings and tutoring sessions.",
                    "/Tutors/TutorDashboard"),
                BcUserRole.SeniorTutor => (
                    "Senior Tutor access granted",
                    "Your account has been updated to Senior Tutor. Your Senior Tutor tools are now available.",
                    "/Tutors/TutorDashboard"),
                BcUserRole.HeadOfTutors => (
                    "Head of Tutors access granted",
                    "Your account has been updated to Head of Tutors. You can now manage tutor activities and review completed sessions.",
                    "/TutorHead/SessionReviews"),
                BcUserRole.Admin => (
                    "Admin access granted",
                    "You have been granted Admin access. Administrative tools are now available from your dashboard.",
                    "/Administrator/Admin/Dashboard"),
                BcUserRole.SuperAdmin => (
                    "Super Admin access granted",
                    "You have been granted Super Admin access. Advanced administration tools are now available.",
                    "/Administrator/Admin/Dashboard"),
                _ => (
                    "Account access updated",
                    "Your Mzala Connect role or access has been updated. Sign in to see the tools available to you.",
                    "/Student/Dashboard")
            };

            UserNotification? platformNotification = ChangeTracker
                .Entries<UserNotification>()
                .Where(notification =>
                    notification.State == EntityState.Added &&
                    notification.Entity.RecipientBcUserId ==
                        entry.Entity.BcUserId)
                .Select(notification => notification.Entity)
                .FirstOrDefault();
            if (platformNotification is null)
            {
                platformNotification = new UserNotification
                {
                    RecipientBcUserId = entry.Entity.BcUserId,
                    Title = title,
                    Message = message,
                    LinkUrl = link,
                    CreatedAt = now
                };
                UserNotifications.Add(platformNotification);
            }

            // When the role change is part of a workflow that already created
            // a notification (for example, approving a tutor for placement),
            // send that same message by email instead of a second generic role
            // update. This keeps both channels in sync for the recipient.
            NotificationEmailQueue.Add(
                this,
                $"role-change:{entry.Entity.BcUserId}:{previous}:{current}:{now.ToUnixTimeMilliseconds()}",
                entry.Entity.Email,
                entry.Entity.DisplayName,
                platformNotification.Title,
                $"{NotificationEmailQueue.Greeting(entry.Entity.DisplayName)}" +
                $"{Environment.NewLine}{Environment.NewLine}{platformNotification.Message}",
                platformNotification.LinkUrl,
                now);
        }
    }

    private static string RoleLabel(BcUserRole role) => role switch
    {
        BcUserRole.SuperAdmin => "Super Admin",
        BcUserRole.HeadOfTutors => "Head of Tutors",
        BcUserRole.SeniorTutor => "Senior Tutor",
        _ => role.ToString()
    };

    private static string RoleHome(BcUserRole role) => role switch
    {
        BcUserRole.Admin or BcUserRole.SuperAdmin =>
            "/Administrator/Admin/Dashboard",
        BcUserRole.HeadOfTutors => "/TutorHead/SessionReviews",
        BcUserRole.Tutor or BcUserRole.SeniorTutor =>
            "/Tutors/TutorDashboard",
        _ => "/"
    };

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
            user.Role is not (BcUserRole.Student or BcUserRole.Tutor or
                BcUserRole.SeniorTutor))
        {
            return;
        }

        bool isActiveTutor = tutorEntry.State != EntityState.Deleted &&
            tutorEntry.Entity.Status == TutorStatus.Approved &&
            tutorEntry.Entity.ApplicationStage == TutorApplicationStage.Placement &&
            tutorEntry.Entity.IsActive;
        user.Role = isActiveTutor
            ? user.Role == BcUserRole.SeniorTutor
                ? BcUserRole.SeniorTutor
                : BcUserRole.Tutor
            : BcUserRole.Student;
    }


    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ApplicationDbContext).Assembly);
    }
}
