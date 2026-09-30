using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using BC_CampusLearn.Pages.TutorHead;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BC_CampusLearn.Tests;

public class TutorHeadSessionReviewsTests
{
    [Fact]
    public async Task PageOnlyShowsCompletedSessionsWithBothReviews()
    {
        await using ApplicationDbContext context = CreateContext();
        TutorCourseModule assignment = CreateAssignment();
        context.TutorCourseModules.Add(assignment);
        context.Bookings.AddRange(
            CreateBooking(
                1,
                assignment,
                "Ready Student",
                BookingStatus.Completed,
                new StudentEvaluation(),
                new TutorStudentEvaluation()),
            CreateBooking(
                2,
                assignment,
                "Missing Tutor Review",
                BookingStatus.Completed,
                new StudentEvaluation(),
                null),
            CreateBooking(
                3,
                assignment,
                "Not Completed",
                BookingStatus.Confirmed,
                new StudentEvaluation(),
                new TutorStudentEvaluation()));
        await context.SaveChangesAsync();

        var page = CreatePage(context);
        await page.OnGetAsync(CancellationToken.None);

        SessionReviewsModel.SessionReviewListItem session =
            Assert.Single(page.Sessions);
        Assert.Equal("Ready Student", session.StudentName);
        Assert.Equal("Taylor Tutor", session.TutorDisplayName);
        Assert.False(session.HasTutorHeadReview);
        Assert.Equal("Awaiting review", session.ReviewStatus);
        Assert.Equal(1, page.PeriodSummary.TotalSessions);
        Assert.Equal(1, page.PeriodSummary.AwaitingReview);
        Assert.Equal(0, page.PeriodSummary.Reviewed);

        SessionReviewsModel filteredPage = CreatePage(context);
        filteredPage.TutorFilter = "Taylor";
        filteredPage.StudentFilter = "Ready";
        filteredPage.DateFrom = new DateOnly(2026, 9, 21);
        filteredPage.DateTo = new DateOnly(2026, 9, 21);
        await filteredPage.OnGetAsync(CancellationToken.None);

        Assert.Single(filteredPage.Sessions);
        Assert.True(filteredPage.HasActiveFilters);
    }

    [Fact]
    public async Task PageMarksSessionReviewedWhenTutorHeadReviewExists()
    {
        await using ApplicationDbContext context = CreateContext();
        TutorCourseModule assignment = CreateAssignment();
        Booking booking = CreateBooking(
            1,
            assignment,
            "Reviewed Student",
            BookingStatus.Completed,
            new StudentEvaluation(),
            new TutorStudentEvaluation());
        var tutorHead = new BcUser
        {
            BcUserId = 2,
            PersonnelNumber = "TH001",
            DisplayName = "Tutor Head",
            Role = BcUserRole.HeadOfTutors
        };
        booking.SessionReviews.Add(new SessionReview
        {
            Booking = booking,
            Reviewer = tutorHead,
            ReviewerBcUserId = tutorHead.BcUserId,
            Rating = 5,
            Comment = "Review complete",
            CreatedAt = DateTimeOffset.UtcNow
        });
        context.TutorCourseModules.Add(assignment);
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var page = CreatePage(context);
        await page.OnGetAsync(CancellationToken.None);

        SessionReviewsModel.SessionReviewListItem session =
            Assert.Single(page.Sessions);
        Assert.True(session.HasTutorHeadReview);
        Assert.Equal("Reviewed", session.ReviewStatus);
        Assert.Equal(1, page.PeriodSummary.TotalSessions);
        Assert.Equal(0, page.PeriodSummary.AwaitingReview);
        Assert.Equal(1, page.PeriodSummary.Reviewed);
    }

    [Fact]
    public async Task SessionDetailsShowsBothReviewsAndRecording()
    {
        await using ApplicationDbContext context = CreateContext();
        TutorCourseModule assignment = CreateAssignment();
        Booking booking = CreateBooking(
            1,
            assignment,
            "Reviewed Student",
            BookingStatus.Completed,
            new StudentEvaluation
            {
                TutoringMode = "Online",
                TutorHelp = "Worked through programming examples",
                ModeRating = 5,
                PlatformRating = 4
            },
            new TutorStudentEvaluation
            {
                SessionPlan = true,
                StudentPreparationInfo = true,
                StudentPunctuality = true,
                StudentPrepared = true,
                StudentInteract = "The student participated well.",
                StudentFocus = "The student remained focused.",
                StudentIssues = "No issues.",
                TutorComments = "Good progress was made.",
                RecordingLink = "https://example.com/session-recording"
            });
        context.TutorCourseModules.Add(assignment);
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        SessionDetailsModel page = CreateDetailsPage(context);
        var result = await page.OnGetAsync(1, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Reviewed Student", page.Session.StudentName);
        Assert.NotEmpty(page.StudentReviewAnswers);
        Assert.NotEmpty(page.TutorReviewAnswers);
        Assert.True(page.CanWatchRecording);
        Assert.Equal(
            "https://example.com/session-recording",
            page.RecordingUrl?.TrimEnd('/'));
    }

    [Fact]
    public async Task TutorHeadCanSaveStructuredSessionReview()
    {
        await using ApplicationDbContext context = CreateContext();
        TutorCourseModule assignment = CreateAssignment();
        Booking booking = CreateBooking(
            1,
            assignment,
            "Reviewed Student",
            BookingStatus.Completed,
            new StudentEvaluation(),
            new TutorStudentEvaluation());
        context.TutorCourseModules.Add(assignment);
        context.BcUsers.Add(new BcUser
        {
            BcUserId = 2,
            PersonnelNumber = "TH001",
            DisplayName = "Tutor Head",
            Role = BcUserRole.HeadOfTutors
        });
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        SessionDetailsModel page = CreateDetailsPage(context);
        page.TutorHeadReviewInput = new TutorHeadSessionReviewInput
        {
            ModuleAndTopicCoverage = "Yes",
            ExplanationClarity = "Excellent",
            SessionStructure = "Yes",
            StudentEngagement = "Partially",
            EvidenceConsistency = "Yes",
            ConcernLevel = "Minor concerns",
            OverallAssessment = "Good",
            Decision = "Approve with feedback",
            AdditionalComments = "Follow up on student engagement."
        };

        IActionResult result = await page.OnPostTutorHeadReviewAsync(
            1,
            CancellationToken.None);

        RedirectToPageResult redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(1, redirect.RouteValues?["id"]);
        SessionReview review = Assert.Single(context.SessionReviews);
        Assert.Equal(2, review.ReviewerBcUserId);
        Assert.Equal(assignment.Tutor.BcUserId, review.RevieweeBcUserId);
        Assert.Equal("Excellent", review.ExplanationClarity);
        Assert.Equal("Approve with feedback", review.Decision);
        Assert.Equal((byte)4, review.Rating);
        Assert.Equal("Follow up on student engagement.", review.Comment);
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static SessionReviewsModel CreatePage(
        ApplicationDbContext context) => new(
            context,
            new FixedTimeProvider(new DateTimeOffset(
                2026, 9, 29, 10, 0, 0, TimeSpan.Zero)));

    private static SessionDetailsModel CreateDetailsPage(
        ApplicationDbContext context) => new(
            context,
            new TestCurrentUserService(new CurrentUser(
                2,
                "TH001",
                "Tutor Head",
                "tutorhead@example.com",
                BcUserRole.HeadOfTutors)),
            new FixedTimeProvider(new DateTimeOffset(
                2026, 9, 29, 10, 0, 0, TimeSpan.Zero)));

    private static TutorCourseModule CreateAssignment()
    {
        var programme = new ProgrammeOfStudy
        {
            Id = 1,
            Name = "Computing"
        };
        var module = new ProgrammeModule
        {
            ProgrammeModuleId = 1,
            Programme = programme,
            ModuleCode = "PROG101",
            ModuleName = "Programming"
        };
        var tutor = new Tutor
        {
            TutorId = 1,
            BcUserId = 1,
            ProgrammeId = 1,
            Programme = programme,
            BcUser = new BcUser
            {
                BcUserId = 1,
                PersonnelNumber = "T001",
                DisplayName = "Taylor Tutor",
                Role = BcUserRole.Tutor
            },
            ReasonForTutoring = "Help students",
            TeachingStyle = "Interactive",
            PreviousTutoringExperience = "Experienced",
            CampusOfStudy = "Pretoria Campus",
            DemonstrationVideoUrl = "https://example.com/video",
            Status = TutorStatus.Approved,
            IsActive = true
        };

        return new TutorCourseModule
        {
            Tutor = tutor,
            ProgrammeModule = module,
            IsActive = true
        };
    }

    private static Booking CreateBooking(
        int id,
        TutorCourseModule assignment,
        string studentName,
        BookingStatus status,
        StudentEvaluation? studentReview,
        TutorStudentEvaluation? tutorReview) => new()
        {
            BookingId = id,
            TutorId = assignment.Tutor.TutorId,
            ProgrammeModuleId =
                assignment.ProgrammeModule.ProgrammeModuleId,
            TutorCourseModule = assignment,
            ProgrammeModule = assignment.ProgrammeModule,
            StudentName = studentName,
            Location = "Online",
            Status = status,
            Duration = SessionDuration.OneHour,
            ScheduledStartTime = new DateTimeOffset(
                2026,
                9,
                20 + id,
                10,
                0,
                0,
                TimeSpan.Zero),
            DateBooked = new DateTimeOffset(
                2026,
                9,
                1,
                10,
                0,
                0,
                TimeSpan.Zero),
            CompletedAt = status == BookingStatus.Completed
                ? new DateTimeOffset(
                    2026,
                    9,
                    20 + id,
                    11,
                    0,
                    0,
                    TimeSpan.Zero)
                : null,
            StudentEvaluation = studentReview,
            TutorEvaluation = tutorReview
        };

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class TestCurrentUserService(CurrentUser user)
        : ICurrentUserService
    {
        public bool IsAuthenticated => true;

        public CurrentUser GetRequiredUser() => user;
    }
}
