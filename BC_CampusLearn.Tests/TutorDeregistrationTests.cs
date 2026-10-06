using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Tutors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace BC_CampusLearn.Tests;

public class TutorDeregistrationTests
{
    [Theory]
    [InlineData(BcUserRole.Tutor)]
    [InlineData(BcUserRole.HeadOfTutors)]
    public async Task DeregistrationDemotesUserAndBlocksDashboardEvenWithStaleRole(BcUserRole role)
    {
        await using var context = await CreateContextAsync();
        (await context.BcUsers.FindAsync(1))!.Role = role;
        await context.SaveChangesAsync();
        Assert.Null(await new TutorDeregistrationService(context, new FixedTimeProvider())
            .DeregisterAsync(1, Administrator, "Term completed"));
        Assert.Equal(BcUserRole.Student, (await context.BcUsers.FindAsync(1))!.Role);
        var page = new BC_CampusLearn.Pages.Tutors.TutorDashboardModel(context,
            new StaleTutorUserService(role), null!);
        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(await page.OnGetAsync(CancellationToken.None));
    }

    private sealed class StaleTutorUserService(BcUserRole role) : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public CurrentUser GetRequiredUser() => new(1, "S1", "Tutor", null, role);
    }

    private static readonly CurrentUser Administrator = new(200, "A200", "Administrator", null, BcUserRole.Admin);
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task DeregistrationPreservesAuditHistoryAndSendsOneEncouragingNotification()
    {
        await using var context = await CreateContextAsync();
        var tutor = await context.Tutors.SingleAsync();
        var assignment = await context.TutorCourseModules.SingleAsync();
        var booking = new Booking
        {
            TutorId = tutor.TutorId, TutorCourseModule = assignment, ProgrammeModuleId = 1,
            StudentBcUserId = 100, StudentName = "Student", Status = BookingStatus.Completed,
            TutorEvaluation = new TutorStudentEvaluation
            {
                TranscriptStoragePath = "App_Data/tutor-review-transcripts/retained.pdf",
                RecordingLink = "https://example.test/recording"
            },
            StudentEvaluation = new StudentEvaluation(),
            Documents = [new BookingDocument { OriginalFileName = "notes.pdf", StoragePath = "App_Data/booking-documents/notes.pdf" }],
            SessionMessages = [new SessionMessage { SenderBcUserId = 1, RecipientBcUserId = 100, MessageText = "Historical conversation" }]
        };
        context.Bookings.Add(booking);
        context.TutorModuleChangeRequests.Add(new TutorModuleChangeRequest
        {
            TutorId = 1, ProgrammeModuleId = 1, Status = TutorAccountRequestStatus.Pending
        });
        context.TutorDeregistrationRequests.Add(new TutorDeregistrationRequest { TutorId = 1 });
        context.ResourceTutorNominations.Add(new ResourceTutorNomination { TutorId = 1, ProgrammeModuleId = 1, NominatedByBcUserId = 200 });
        await context.SaveChangesAsync();

        var service = new TutorDeregistrationService(context, new FixedTimeProvider());
        Assert.Null(await service.DeregisterAsync(1, Administrator, " Programme participation ended. "));
        tutor = await context.Tutors.Include(t => t.BcUser).SingleAsync();
        assignment = await context.TutorCourseModules.SingleAsync();
        Assert.Equal(TutorStatus.Deregistered, tutor.Status);
        Assert.False(tutor.IsActive);
        Assert.Equal(BcUserRole.Student, tutor.BcUser.Role);
        Assert.Equal(Now, tutor.DeregisteredAt);
        Assert.Equal(200, tutor.DeregisteredByBcUserId);
        Assert.Equal("Administrator", tutor.DeregisteredByName);
        Assert.Equal("Programme participation ended.", tutor.DeregistrationReason);
        Assert.False(assignment.IsActive);
        Assert.Empty(await context.TutorAvailabilities.ToListAsync());
        Assert.Equal(BookingStatus.Completed, (await context.Bookings.SingleAsync()).Status);
        Assert.Equal("Historical conversation", (await context.SessionMessages.SingleAsync()).MessageText);
        Assert.Equal("App_Data/tutor-review-transcripts/retained.pdf", (await context.TutorStudentEvaluations.SingleAsync()).TranscriptStoragePath);
        Assert.Single(await context.StudentEvaluations.ToListAsync());
        Assert.Single(await context.BookingDocuments.ToListAsync());
        Assert.Equal(TutorAccountRequestStatus.Declined, (await context.TutorModuleChangeRequests.SingleAsync()).Status);
        Assert.Equal(TutorAccountRequestStatus.Approved, (await context.TutorDeregistrationRequests.SingleAsync()).Status);
        Assert.False((await context.ResourceTutorNominations.SingleAsync()).IsActive);
        var notification = await context.UserNotifications.SingleAsync();
        Assert.Equal(1, notification.RecipientBcUserId);
        Assert.Contains("Thank you", notification.Message);
        Assert.Contains("every success", notification.Message);
        Assert.Equal("/Tutors/Sessions", notification.LinkUrl);
        Assert.Equal(Now, notification.CreatedAt);
        Assert.NotNull(await service.DeregisterAsync(1, Administrator, "Repeated request"));
        Assert.Single(await context.UserNotifications.ToListAsync());
        Assert.Equal("Programme participation ended.", tutor.DeregistrationReason);
    }

    [Theory]
    [InlineData(BookingStatus.Pending)]
    [InlineData(BookingStatus.Confirmed)]
    [InlineData(BookingStatus.InProgress)]
    public async Task OpenSessionsBlockDeregistrationWithoutChangingData(BookingStatus status)
    {
        await using var context = await CreateContextAsync();
        context.Bookings.Add(new Booking
        {
            TutorId = 1, ProgrammeModuleId = 1, Status = status,
            TutorCourseModule = await context.TutorCourseModules.SingleAsync()
        });
        await context.SaveChangesAsync();
        Assert.Contains("Resolve", await new TutorDeregistrationService(context, new FixedTimeProvider())
            .DeregisterAsync(1, Administrator, "End registration"));
        var tutor = await context.Tutors.SingleAsync();
        Assert.Equal(TutorStatus.Approved, tutor.Status);
        Assert.True(tutor.IsActive);
        Assert.Null(tutor.DeregisteredAt);
        Assert.Single(await context.TutorAvailabilities.ToListAsync());
        Assert.True((await context.TutorCourseModules.SingleAsync()).IsActive);
        Assert.Equal(status, (await context.Bookings.SingleAsync()).Status);
        Assert.Empty(await context.UserNotifications.ToListAsync());
    }

    [Theory]
    [InlineData(BcUserRole.Student, "End registration")]
    [InlineData(BcUserRole.Tutor, "End registration")]
    [InlineData(BcUserRole.Admin, " ")]
    public async Task InvalidRequestsCannotDeregisterTutor(BcUserRole role, string reason)
    {
        await using var context = await CreateContextAsync();
        Assert.NotNull(await new TutorDeregistrationService(context, new FixedTimeProvider())
            .DeregisterAsync(1, Administrator with { Role = role }, reason));
        Assert.True((await context.Tutors.SingleAsync()).IsActive);
        Assert.Empty(await context.UserNotifications.ToListAsync());
    }

    [Fact]
    public void DeregistrationMigrationMatchesRuntimeModel()
    {
        using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(local);Database=ModelValidation;Trusted_Connection=True;TrustServerCertificate=True").Options);
        var snapshot = context.GetService<IMigrationsAssembly>().ModelSnapshot!.Model;
        var initialized = context.GetService<IModelRuntimeInitializer>().Initialize(snapshot, designTime: true);
        var differences = context.GetService<IMigrationsModelDiffer>().GetDifferences(
            initialized.GetRelationalModel(), context.GetService<IDesignTimeModel>().Model.GetRelationalModel());
        Assert.True(differences.Count == 0, string.Join("; ", differences.Select(d =>
            d.GetType().Name + ": " + d.GetType().GetProperty("Name")?.GetValue(d) + " " + d.GetType().GetProperty("Table")?.GetValue(d))));
        Assert.Contains("20261006123000_AddTutorDeregistration", context.Database.GetMigrations());
    }

    private static async Task<ApplicationDbContext> CreateContextAsync()
    {
        var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var programme = new ProgrammeOfStudy { Id = 1, Name = "Computing" };
        var module = new ProgrammeModule { ProgrammeModuleId = 1, Programme = programme, ModuleCode = "PRG101", ModuleName = "Programming" };
        context.Tutors.Add(new Tutor
        {
            TutorId = 1, BcUser = new BcUser { BcUserId = 1, DisplayName = "Tutor", PersonnelNumber = "S1", Role = BcUserRole.Tutor },
            Programme = programme, CampusOfStudy = "Pretoria", YearOfStudy = 2,
            ReasonForTutoring = "", TeachingStyle = "", PreviousTutoringExperience = "", DemonstrationVideoUrl = "",
            Status = TutorStatus.Approved, ApplicationStage = TutorApplicationStage.Placement, IsActive = true,
            TutorCourseModules = [new TutorCourseModule { ProgrammeModule = module }],
            TutorAvailabilities = [new TutorAvailability { AvailableTime = Now.AddDays(1) }]
        });
        context.BcUsers.Add(new BcUser { BcUserId = 100, DisplayName = "Student", PersonnelNumber = "S100" });
        context.BcUsers.Add(new BcUser { BcUserId = 200, DisplayName = "Administrator", PersonnelNumber = "A200", Role = BcUserRole.Admin });
        await context.SaveChangesAsync();
        return context;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
