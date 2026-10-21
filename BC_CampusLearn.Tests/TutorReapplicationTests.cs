using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Tutors;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BC_CampusLearn.Tests;

public class TutorReapplicationTests
{
    [Theory]
    [InlineData(TutorApplicationStage.Submitted)]
    [InlineData(TutorApplicationStage.Shortlisted)]
    [InlineData(TutorApplicationStage.Interview)]
    public async Task RejectionRestoresDeregisteredStatusAndPreservesHistory(TutorApplicationStage stage)
    {
        await using var context = await CreateContextAsync(stage);
        var result = stage switch
        {
            TutorApplicationStage.Submitted => await TutorApplicationReview.RejectAsync(context, 1, "Unsuccessful", default),
            TutorApplicationStage.Shortlisted => await TutorApplicationReview.RejectShortlistedAsync(context, 1, "Unsuccessful", default, 200),
            _ => await TutorApplicationReview.RejectInterviewedAsync(context, 1, "Unsuccessful", default, 200)
        };
        Assert.True(result.Succeeded, result.Message);
        await AssertDeregisteredHistoryAsync(context);
        Assert.Equal("Tutor application unsuccessful", (await context.UserNotifications.SingleAsync()).Title);

        (await context.TutorApplicationSettings.SingleAsync()).IsOpen = false;
        await context.SaveChangesAsync();
        Assert.Equal(0, await TutorApplicationReview.RemoveRejectedApplicationsWhenCycleClosedAsync(context, default));
        await AssertDeregisteredHistoryAsync(context);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BulkRejectionRetainsReturningTutor(bool selected)
    {
        await using var context = await CreateContextAsync(TutorApplicationStage.Submitted);
        var result = selected
            ? await TutorApplicationReview.RejectSelectedAsync(context, [1], default, 200)
            : await TutorApplicationReview.RejectRemainingAsync(context, default, 200);
        Assert.True(result.Succeeded, result.Message);
        await AssertDeregisteredHistoryAsync(context);
        Assert.Equal("Tutor application unsuccessful", (await context.UserNotifications.SingleAsync()).Title);
    }

    [Fact]
    public async Task PlacementReactivatesOnlyNewSelectionsAndAnotherDeregistrationAppendsHistory()
    {
        await using var context = await CreateContextAsync(TutorApplicationStage.Submitted);
        Assert.True((await TutorApplicationReview.ShortlistAsync(context, 1, "Eligible", default, 200)).Succeeded);
        Assert.False((await context.Tutors.SingleAsync()).IsActive);
        Assert.True((await TutorApplicationReview.MoveToInterviewAsync(context, 1,
            new InterviewPreparationDetails(null, null, null, null, null), default, 200)).Succeeded);
        Assert.Equal(BcUserRole.Student, (await context.BcUsers.SingleAsync(u => u.BcUserId == 1)).Role);
        Assert.True((await TutorApplicationReview.MoveInterviewToPlacementAsync(context, 1, default, 200)).Succeeded);
        var tutor = await context.Tutors.Include(t => t.BcUser).SingleAsync();
        Assert.Equal(TutorStatus.Approved, tutor.Status);
        Assert.Equal(TutorApplicationStage.Placement, tutor.ApplicationStage);
        Assert.True(tutor.IsActive);
        Assert.False(tutor.IsReapplication);
        Assert.Equal(BcUserRole.Tutor, tutor.BcUser.Role);
        Assert.False((await context.TutorCourseModules.SingleAsync(a => a.ProgrammeModuleId == 1)).IsActive);
        Assert.All(await context.TutorCourseModules.Where(a => a.ProgrammeModuleId != 1).ToListAsync(), a => Assert.True(a.IsActive));
        Assert.Empty(await context.TutorAvailabilities.ToListAsync());

        Assert.Null(await new TutorDeregistrationService(context, TimeProvider.System).DeregisterAsync(1,
            new CurrentUser(200, "A200", "Administrator", "admin@example.test", BcUserRole.Admin), "Second term ended"));
        var history = await context.TutorDeregistrationEvents.OrderBy(e => e.TutorDeregistrationEventId).ToListAsync();
        Assert.Equal(2, history.Count);
        Assert.Equal("First term ended", history[0].Reason);
        Assert.Equal("Second term ended", history[1].Reason);
        Assert.Equal(BookingStatus.Completed, (await context.Bookings.SingleAsync()).Status);

        var page = new BC_CampusLearn.Pages.Administrator.Tutors.ProfileModel(context);
        await page.OnGetAsync(1, default);
        Assert.Equal(2, page.Tutor.DeregistrationHistory.Count);
    }

    [Fact]
    public async Task AdministratorsReviewOnlyNewModulesAndDocuments()
    {
        await using var context = await CreateContextAsync(TutorApplicationStage.Submitted);
        var page = new BC_CampusLearn.Pages.Administrator.Admin.ApplicationsModel(context);
        await page.OnGetAsync(default);
        var candidate = Assert.Single(page.Candidates);
        Assert.Equal(2, candidate.ModuleCount);
        Assert.Equal(2, candidate.Modules.Count);
        Assert.DoesNotContain(candidate.Modules, module => module.Code == "M1");
        Assert.Equal("new.pdf", Assert.Single(candidate.Documents).OriginalFileName);
    }

    private static async Task AssertDeregisteredHistoryAsync(ApplicationDbContext context)
    {
        var tutor = await context.Tutors.Include(t => t.BcUser).SingleAsync();
        Assert.Equal(TutorStatus.Deregistered, tutor.Status);
        Assert.False(tutor.IsActive);
        Assert.False(tutor.IsReapplication);
        Assert.Equal(BcUserRole.Student, tutor.BcUser.Role);
        Assert.Equal("First term ended", tutor.DeregistrationReason);
        Assert.Single(await context.TutorDeregistrationEvents.ToListAsync());
        Assert.Equal(BookingStatus.Completed, (await context.Bookings.SingleAsync()).Status);
        Assert.Equal(3, await context.TutorCourseModules.CountAsync());
        Assert.All(await context.TutorCourseModules.ToListAsync(), a => Assert.False(a.IsActive));
        Assert.Equal(2, await context.TutorDocuments.CountAsync());
    }

    private static async Task<ApplicationDbContext> CreateContextAsync(TutorApplicationStage stage)
    {
        var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var programme = new ProgrammeOfStudy { Id = 1, Name = "Computing" };
        var tutor = new Tutor
        {
            TutorId = 1, BcUser = new BcUser { BcUserId = 1, DisplayName = "Returning Tutor", PersonnelNumber = "S1" },
            Programme = programme, CampusOfStudy = "Pretoria", ReasonForTutoring = "New reason", TeachingStyle = "Patient",
            PreviousTutoringExperience = "Previous term", DemonstrationVideoUrl = "https://example.test/video",
            Status = TutorStatus.Pending, ApplicationStage = stage, IsReapplication = true,
            DeregisteredAt = DateTimeOffset.UtcNow.AddMonths(-2), DeregistrationReason = "First term ended",
            SubmittedAt = DateTime.UtcNow,
            TutorDocuments =
            [
                new TutorDocument { FilePath = "App_Data/tutor-documents/old.pdf", OriginalFileName = "old.pdf", IsArchived = true },
                new TutorDocument { FilePath = "App_Data/tutor-documents/new.pdf", OriginalFileName = "new.pdf" }
            ],
            DeregistrationHistory = [new TutorDeregistrationEvent
            {
                DeregisteredAt = DateTimeOffset.UtcNow.AddMonths(-2), AdministratorBcUserId = 200,
                AdministratorName = "Administrator", Reason = "First term ended"
            }]
        };
        for (int i = 1; i <= 3; i++) tutor.TutorCourseModules.Add(new TutorCourseModule
        {
            ProgrammeModule = new ProgrammeModule { ProgrammeModuleId = i, Programme = programme, ModuleCode = $"M{i}", ModuleName = $"Module {i}" },
            IsActive = false, IsReapplicationSelection = i != 1
        });
        context.Tutors.Add(tutor);
        context.BcUsers.Add(new BcUser { BcUserId = 200, DisplayName = "Administrator", Role = BcUserRole.Admin });
        context.TutorApplicationSettings.Add(new TutorApplicationSettings { IsOpen = true });
        context.Bookings.Add(new Booking
        {
            TutorId = tutor.TutorId, TutorCourseModule = tutor.TutorCourseModules.First(), ProgrammeModuleId = 1,
            Status = BookingStatus.Completed, StudentName = "Historical Student", StudentEmail = "student@example.test",
            ScheduledStartTime = DateTimeOffset.UtcNow.AddMonths(-3)
        });
        await context.SaveChangesAsync();
        return context;
    }
}
