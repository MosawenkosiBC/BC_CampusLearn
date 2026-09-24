using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Pages.Administrator.Admin;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BC_CampusLearn.Tests;

public class AdminStatisticsTests
{
    private static readonly DateTimeOffset Today =
        new(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task StatisticsShowCurrentCoverageAndOverallActivity()
    {
        await using var context = CreateContext();
        ProgrammeOfStudy programme = new() { Id = 1, Name = "Computing" };
        ProgrammeModule[] modules =
        [
            new() { ProgrammeModuleId = 1, Programme = programme, ModuleCode = "A101", ModuleName = "Algorithms" },
            new() { ProgrammeModuleId = 2, Programme = programme, ModuleCode = "B101", ModuleName = "Business" },
            new() { ProgrammeModuleId = 3, Programme = programme, ModuleCode = "C101", ModuleName = "Cloud" }
        ];
        Tutor active = CreateTutor(1, TutorStatus.Approved, true);
        Tutor inactive = CreateTutor(2, TutorStatus.Approved, false);
        Tutor pending = CreateTutor(3, TutorStatus.Pending, false);
        context.Tutors.AddRange(active, inactive, pending);
        context.BcUsers.AddRange(
            new BcUser { BcUserId = 101, PersonnelNumber = "S101", Role = BcUserRole.Student },
            new BcUser { BcUserId = 102, PersonnelNumber = "S102", Role = BcUserRole.Student },
            new BcUser { BcUserId = 103, PersonnelNumber = "S103", Role = BcUserRole.Student },
            new BcUser { BcUserId = 900, PersonnelNumber = "A900", Role = BcUserRole.Admin });
        TutorCourseModule[] assignments =
        [
            new() { Tutor = active, ProgrammeModule = modules[0] },
            new() { Tutor = inactive, ProgrammeModule = modules[1] },
            new() { Tutor = pending, ProgrammeModule = modules[2] }
        ];
        context.TutorCourseModules.AddRange(assignments);
        context.Bookings.AddRange(
            NewBooking(assignments[0], Today.AddDays(-5), BookingStatus.Completed, 101,
                new StudentEvaluation { ModeRating = 5, PlatformRating = 4 }, new TutorStudentEvaluation(),
                new AdminSessionReview { ReviewerBcUserId = 900 }),
            NewBooking(assignments[0], Today.AddDays(-3), BookingStatus.Completed, 102),
            NewBooking(assignments[1], Today.AddDays(-2), BookingStatus.Cancelled, 103),
            NewBooking(assignments[2], Today.AddDays(3), BookingStatus.Confirmed, 104),
            NewBooking(assignments[0], Today.AddDays(-120), BookingStatus.Completed, 105));
        await context.SaveChangesAsync();

        var page = new StatisticsModel(context, new FixedTimeProvider());
        await page.OnGetAsync(CancellationToken.None);

        Assert.Equal(3, page.TotalTutorCount);
        Assert.Equal(1, page.ActiveTutorCount);
        var campus = Assert.Single(page.TutorsByCampus);
        Assert.Equal("Pretoria", campus.Campus);
        Assert.Equal(1, campus.TutorCount);
        Assert.Equal(1, page.MaxCampusTutorCount);
        Assert.Equal(1, page.PendingApplicationCount);
        Assert.Equal(3, page.TotalModuleCount);
        Assert.Equal(1, page.CoveredModuleCount);
        Assert.Equal(4, page.TotalSessionCount);
        Assert.Equal(4, page.OutcomeSessionCount);
        Assert.DoesNotContain(page.SessionStatuses,
            status => status.Status == BookingStatus.Pending);
        Assert.Equal("#7fbe8c", page.SessionStatuses
            .Single(status => status.Status == BookingStatus.Confirmed).Color);
        Assert.Equal("#75add0", page.SessionStatuses
            .Single(status => status.Status == BookingStatus.Completed).Color);
        Assert.Equal("#d85c5c", page.SessionStatuses
            .Single(status => status.Status == BookingStatus.Cancelled).Color);
        Assert.Equal("#d85c5c", page.SessionStatuses
            .Single(status => status.Status == BookingStatus.Declined).Color);
        Assert.Equal("#9b78bd", page.SessionStatuses
            .Single(status => status.Status == BookingStatus.InProgress).Color);
        Assert.Equal(3, page.TotalBookedSessions);
        Assert.Equal(new[] { ("A101", 3) },
            page.BookedModules.Select(module => (module.Code, module.Count)));
        Assert.Single(page.TopBookedModules);
        Assert.Equal(3, page.CompletedSessionCount);
        var topTutor = Assert.Single(page.TopCompletedTutors);
        Assert.Equal(3, topTutor.Count);
        Assert.Contains("100%", page.TopCompletedTutorPieGradient);
        Assert.Equal(1, page.CancelledSessionCount);
        Assert.Equal(3, page.TotalStudentCount);
        Assert.Equal("A101", Assert.Single(page.AssignedModules).Code);
        Assert.Equal(1, page.BothReviewsCompleteCount);
        Assert.Equal(2, page.PendingStudentReviewCount);
        Assert.Equal(2, page.PendingTutorReviewCount);
        Assert.Equal(0, page.PendingTutorHeadReviewCount);
        Assert.Equal(1, page.AwaitingAdminReviewCount);
        Assert.Equal(3, page.UniqueBookingStudentCount);
        Assert.Equal(5d, page.AverageTutorRating);
        Assert.Equal(4d, page.AveragePlatformRating);
        Assert.Equal(3d / 4d * 100d, page.CompletionRate, 3);

        page.BookingPeriod = "weekly";
        await page.OnGetAsync(CancellationToken.None);

        Assert.Equal(3, page.TotalBookedSessions);
        Assert.Equal(2, page.FilteredBookedSessionCount);
        Assert.Equal(2, Assert.Single(page.BookedModules).Count);
        Assert.Equal(2, Assert.Single(page.TopCompletedTutors).Count);
        Assert.Equal("Past 7 days", page.BookingPeriodLabel);

        page.ReviewPeriod = "weekly";
        await page.OnGetAsync(CancellationToken.None);

        Assert.Equal(2, page.UniqueBookingStudentCount);
        Assert.Equal(1, page.PendingTutorReviewCount);
        Assert.Equal(1, page.AwaitingAdminReviewCount);
    }

    [Fact]
    public async Task MostBookedModulesShowFivePlusOtherAndModalHasEveryModule()
    {
        await using var context = CreateContext();
        ProgrammeOfStudy programme = new() { Id = 1, Name = "Computing" };
        Tutor activeTutor = CreateTutor(1, TutorStatus.Approved, true);
        context.Tutors.Add(activeTutor);
        for (int id = 1; id <= 6; id++)
        {
            var module = new ProgrammeModule
            {
                ProgrammeModuleId = id, Programme = programme,
                ModuleCode = $"MOD{id}", ModuleName = $"Module {id}"
            };
            context.TutorCourseModules.Add(new TutorCourseModule
            {
                Tutor = activeTutor,
                ProgrammeModule = module
            });
            for (int count = 0; count < 7 - id; count++)
            {
                context.Bookings.Add(new Booking
                {
                    ProgrammeModule = module,
                    ProgrammeModuleId = id,
                    TutorId = 1,
                    ScheduledStartTime = Today.AddDays(-1),
                    Status = BookingStatus.Completed
                });
            }
        }
        await context.SaveChangesAsync();

        var page = new StatisticsModel(context, new FixedTimeProvider());
        await page.OnGetAsync(CancellationToken.None);

        Assert.Equal(21, page.TotalBookedSessions);
        Assert.Equal(6, page.AssignedModules.Count);
        Assert.Equal(5, page.TopAssignedModules.Count);
        Assert.DoesNotContain(page.TopAssignedModules, module => module.Code == "MOD6");
        Assert.Equal(6, page.BookedModules.Count);
        Assert.Equal(5, page.TopBookedModules.Count(module => module.Name != "Other modules"));
        Assert.Equal(1, page.TopBookedModules[^1].Count);
        Assert.Equal("Other modules", page.TopBookedModules[^1].Name);
        Assert.Contains("100%", page.TopPieGradient);
        Assert.Contains("100%", page.AllPieGradient);
    }

    [Fact]
    public async Task StatisticsHandleEmptyData()
    {
        await using var context = CreateContext();
        var page = new StatisticsModel(context, new FixedTimeProvider());

        await page.OnGetAsync(CancellationToken.None);

        Assert.Equal(0, page.TotalSessionCount);
        Assert.Equal(0, page.CompletionRate);
        Assert.Null(page.AverageTutorRating);
        Assert.Null(page.AveragePlatformRating);
        Assert.Empty(page.AssignedModules);
        Assert.Empty(page.TopAssignedModules);
        Assert.Empty(page.TutorsByCampus);
        Assert.Equal(0, page.MaxCampusTutorCount);
        Assert.Empty(page.BookedModules);
        Assert.Empty(page.TopCompletedTutors);
        Assert.Equal("none", page.TopCompletedTutorPieGradient);
        Assert.Equal("none", page.TopPieGradient);
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Tutor CreateTutor(int id, TutorStatus status, bool active) => new()
    {
        TutorId = id,
        BcUserId = id,
        ProgrammeId = 1,
        Status = status,
        IsActive = active,
        ApplicationStage = TutorApplicationStage.Placement,
        ReasonForTutoring = "Reason",
        TeachingStyle = "Style",
        PreviousTutoringExperience = "Experience",
        CampusOfStudy = "Pretoria",
        DemonstrationVideoUrl = ""
    };

    private static Booking NewBooking(
        TutorCourseModule assignment, DateTimeOffset date, BookingStatus status,
        int studentId, StudentEvaluation? studentReview = null,
        TutorStudentEvaluation? tutorReview = null,
        AdminSessionReview? adminReview = null) => new()
    {
        TutorCourseModule = assignment,
        TutorId = assignment.Tutor.TutorId,
        ProgrammeModule = assignment.ProgrammeModule,
        ProgrammeModuleId = assignment.ProgrammeModule.ProgrammeModuleId,
        ScheduledStartTime = date,
        DateBooked = date.AddDays(-1),
        Status = status,
        StudentBcUserId = studentId,
        StudentEvaluation = studentReview,
        TutorEvaluation = tutorReview,
        AdminSessionReview = adminReview
    };

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Today;
    }
}
