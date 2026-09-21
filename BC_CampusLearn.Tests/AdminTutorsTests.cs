using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Pages.Administrator.Tutors;
using BC_CampusLearn.Models.ViewModels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.ViewFeatures.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Xunit;

namespace BC_CampusLearn.Tests;

public class AdminTutorsTests
{
    [Fact]
    public async Task AdminReviewSavesFiveAnswersAndRecordingTime()
    {
        await using var context = CreateContext();
        await SeedTutors(context);
        TutorCourseModule assignment = await context.TutorCourseModules.FirstAsync();
        Booking booking = new()
        {
            TutorId = assignment.TutorId,
            TutorCourseModule = assignment,
            ProgrammeModuleId = assignment.ProgrammeModuleId,
            Status = BookingStatus.Completed,
            StudentEvaluation = new StudentEvaluation(),
            TutorEvaluation = new TutorStudentEvaluation()
        };
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        DateTimeOffset recordedAt = new(2026, 9, 21, 10, 30, 0, TimeSpan.Zero);
        var page = new SessionDetailsModel(
            context, new TestWebHostEnvironment(), new TestCurrentUserService(),
            new TestTimeProvider(recordedAt))
        {
            AdminReviewInput = new AdminSessionReviewInput
            {
                AllReviewsSubmitted = true,
                HeadConfirmedSession = false,
                HeadConfirmedQuality = false,
                ConcernsResolvedOrDocumented = true,
                EvidenceSupportsApproval = false
            }
        };
        SetPageContext(page);

        Assert.IsType<RedirectToPageResult>(
            await page.OnPostAdminReviewAsync(booking.BookingId, CancellationToken.None));

        AdminSessionReview saved = await context.AdminSessionReviews.SingleAsync();
        Assert.Equal(booking.BookingId, saved.BookingId);
        Assert.Equal(1, saved.ReviewerBcUserId);
        Assert.True(saved.AllReviewsSubmitted);
        Assert.False(saved.HeadConfirmedSession);
        Assert.False(saved.HeadConfirmedQuality);
        Assert.True(saved.ConcernsResolvedOrDocumented);
        Assert.False(saved.EvidenceSupportsApproval);
        Assert.Equal(recordedAt, saved.RecordedAt);
    }

    [Fact]
    public async Task ProfilePaginatesSessionsByReviewCompletenessBeforeDate()
    {
        await using var context = CreateContext();
        await SeedTutors(context);
        TutorCourseModule assignment = await context.TutorCourseModules.FirstAsync();
        DateTimeOffset now = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(2));
        DateTimeOffset date = new(now.Year, now.Month, 15, 12, 0, 0, now.Offset);
        Booking[] bookings =
        [
            new() { ScheduledStartTime = date.AddHours(5) },
            new() { ScheduledStartTime = date.AddHours(4), TutorEvaluation = new TutorStudentEvaluation() },
            new() { ScheduledStartTime = date.AddHours(1), StudentEvaluation = new StudentEvaluation(), TutorEvaluation = new TutorStudentEvaluation() },
            new() { ScheduledStartTime = date.AddHours(3), StudentEvaluation = new StudentEvaluation() },
            new() { ScheduledStartTime = date.AddHours(2), StudentEvaluation = new StudentEvaluation(), TutorEvaluation = new TutorStudentEvaluation() },
            new() { ScheduledStartTime = date }
        ];
        foreach (Booking booking in bookings)
        {
            booking.TutorId = assignment.TutorId;
            booking.TutorCourseModule = assignment;
            booking.ProgrammeModuleId = assignment.ProgrammeModuleId;
            booking.Status = BookingStatus.Completed;
        }
        context.Bookings.AddRange(bookings);
        await context.SaveChangesAsync();

        var page = new ProfileModel(context);
        await page.OnGetAsync(assignment.TutorId, CancellationToken.None);
        Assert.Equal(new[] { bookings[4], bookings[2], bookings[1], bookings[3], bookings[0] }
            .Select(item => item.BookingId), page.RecentSessions.Select(item => item.BookingId));

        page.SessionPage = 2;
        await page.OnGetAsync(assignment.TutorId, CancellationToken.None);
        Assert.Equal(bookings[5].BookingId, Assert.Single(page.RecentSessions).BookingId);
    }

    [Theory]
    [InlineData("day")]
    [InlineData("week")]
    [InlineData("month")]
    public async Task ProfileChartCountsOnlyCompletedSessionsInPeriodAndReturnsTopFive(string period)
    {
        await using var context = CreateContext();
        await SeedTutors(context);
        var now = DateTimeOffset.UtcNow;
        for (int moduleId = 2; moduleId <= 7; moduleId++)
        {
            var module = new ProgrammeModule { ProgrammeModuleId = moduleId, ProgrammeId = 1, ModuleCode = $"MOD{moduleId}", ModuleName = $"Module {moduleId}" };
            var assignment = new TutorCourseModule { TutorId = 1, ProgrammeModule = module };
            context.TutorCourseModules.Add(assignment);
            for (int session = 0; session < moduleId; session++)
            {
                context.Bookings.Add(new Booking { TutorId = 1, TutorCourseModule = assignment, ProgrammeModule = module, Status = BookingStatus.Completed, CompletedAt = now, ScheduledStartTime = now });
            }
            context.Bookings.Add(new Booking { TutorId = 1, TutorCourseModule = assignment, ProgrammeModule = module, Status = BookingStatus.Cancelled, CompletedAt = now, ScheduledStartTime = now });
            context.Bookings.Add(new Booking { TutorId = 1, TutorCourseModule = assignment, ProgrammeModule = module, Status = BookingStatus.Completed, CompletedAt = now.AddMonths(-2), ScheduledStartTime = now.AddMonths(-2) });
        }
        await context.SaveChangesAsync();
        var existingBooking = await context.Bookings.FirstAsync();
        var hiddenBooking = new Booking
        {
            TutorId = 1, ProgrammeModuleId = existingBooking.ProgrammeModuleId,
            Status = BookingStatus.Confirmed, ScheduledStartTime = now.AddDays(1)
        };
        context.Bookings.Add(hiddenBooking);
        await context.SaveChangesAsync();
        var page = new ProfileModel(context) { Period = period };
        await page.OnGetAsync(1, CancellationToken.None);
        Assert.Equal(new[] { 7, 6, 5, 4, 3 }, page.TopModules.Select(module => module.Count));
        Assert.Equal(27, page.CompletedSessions);
        Assert.Equal(27, page.PendingStudentReviews);
        Assert.Equal(5, page.RecentSessions.Count);
        Assert.All(page.RecentSessions, item => Assert.Contains(item.Status, new[] { BookingStatus.Completed, BookingStatus.Cancelled }));
        var details = new SessionDetailsModel(
            context, new TestWebHostEnvironment(),
            new TestCurrentUserService(), TimeProvider.System);
        Assert.IsType<Microsoft.AspNetCore.Mvc.RazorPages.PageResult>(await details.OnGetAsync(existingBooking.BookingId, CancellationToken.None));
        Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundResult>(await details.OnGetAsync(hiddenBooking.BookingId, CancellationToken.None));
        Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundResult>(await details.OnGetAsync(int.MaxValue, CancellationToken.None));
        Assert.Equal(7, page.TotalSessionPages);
        var firstPageIds = page.RecentSessions.Select(item => item.BookingId).ToArray();
        page.SessionPage = 2;
        await page.OnGetAsync(1, CancellationToken.None);
        Assert.Equal(5, page.RecentSessions.Count);
        Assert.DoesNotContain(page.RecentSessions, item => firstPageIds.Contains(item.BookingId));
        page.SessionPage = int.MaxValue;
        await page.OnGetAsync(1, CancellationToken.None);
        Assert.Equal(7, page.SessionPage);
        Assert.Equal(3, page.RecentSessions.Count);
        Assert.Null(page.AverageRating);
        await page.OnGetAsync(18, CancellationToken.None);
        Assert.Empty(page.TopModules);
    }

    [Fact]
    public async Task PendingTutorReviewsExcludeReviewedUncompletedAndOutOfPeriodSessions()
    {
        await using var context = CreateContext();
        await SeedTutors(context);
        var date = new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.FromHours(2));
        var assignment = await context.TutorCourseModules.FirstAsync(item => item.TutorId == 1);
        foreach (var kind in new[] { "pending", "reviewed", "cancelled", "outside" })
        {
            context.Bookings.Add(new Booking
            {
                TutorId = 1, TutorCourseModule = assignment, ProgrammeModuleId = 1,
                Status = kind == "cancelled" ? BookingStatus.Cancelled : BookingStatus.Completed,
                CompletedAt = date,
                ScheduledStartTime = kind == "outside" ? date.AddDays(-1) : date,
                TutorEvaluation = kind == "reviewed" ? new TutorStudentEvaluation() : null
            });
        }
        await context.SaveChangesAsync();
        var page = new ProfileModel(context)
        {
            Period = "custom", StartDate = new DateOnly(2026, 9, 10), EndDate = new DateOnly(2026, 9, 10)
        };
        await page.OnGetAsync(1, CancellationToken.None);
        Assert.Equal(1, page.PendingTutorReviews);
        Assert.Equal(2, page.PendingStudentReviews);
        Assert.Equal(2, page.CompletedSessions);
    }

    [Fact]
    public async Task DefaultMonthAndEachPresetFilterEverySummaryCard()
    {
        await using var context = CreateContext();
        await SeedTutors(context);
        var now = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(2));
        var today = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset);
        var assignment = await context.TutorCourseModules.FirstAsync(item => item.TutorId == 1);
        for (int days = -40; days <= 1; days++)
        {
            var date = today.AddDays(days).AddHours(12);
            context.Bookings.Add(new Booking
            {
                TutorId = 1, TutorCourseModule = assignment, ProgrammeModuleId = 1,
                Status = BookingStatus.Completed, CompletedAt = now, ScheduledStartTime = date
            });
            context.TutorModuleChangeRequests.Add(new TutorModuleChangeRequest
            {
                TutorId = 1, ProgrammeModuleId = 1, Status = TutorAccountRequestStatus.Pending,
                SubmittedAt = date.UtcDateTime
            });
        }
        await context.SaveChangesAsync();
        var page = new ProfileModel(context);
        Assert.Equal("month", page.Period);
        foreach (var period in new[] { "month", "week", "day" })
        {
            page.Period = period;
            await page.OnGetAsync(1, CancellationToken.None);
            var start = period switch
            {
                "day" => today,
                "week" => today.AddDays(-((int)today.DayOfWeek + 6) % 7),
                _ => new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, now.Offset)
            };
            var end = period == "day" ? start.AddDays(1) : period == "week" ? start.AddDays(7) : start.AddMonths(1);
            var expected = Enumerable.Range(-40, 42).Count(days => today.AddDays(days).AddHours(12) >= start
                && today.AddDays(days).AddHours(12) < end);
            Assert.Equal(expected, page.CompletedSessions);
            Assert.Equal(expected, page.PendingStudentReviews);
            Assert.Equal(expected, page.PendingTutorReviews);
            Assert.Equal(expected, Assert.Single(page.TopModules).Count);
            Assert.Equal(expected, page.ModuleChangeRequests);
        }
    }

    [Fact]
    public async Task CustomDatesFilterAllActivityUsingInclusiveSouthAfricanDates()
    {
        await using var context = CreateContext();
        await SeedTutors(context);
        var start = new DateTimeOffset(2026, 9, 10, 0, 0, 0, TimeSpan.FromHours(2));
        var end = start.AddDays(2);
        var assignment = await context.TutorCourseModules.FirstAsync(item => item.TutorId == 1);
        foreach (var date in new[] { start.AddTicks(-1), start, end.AddTicks(-1), end })
        {
            var booking = new Booking
            {
                TutorId = 1, TutorCourseModule = assignment, ProgrammeModuleId = 1,
                Status = BookingStatus.Completed, CompletedAt = date.ToUniversalTime(), ScheduledStartTime = date.ToUniversalTime()
            };
            context.Bookings.Add(booking);
            context.StudentEvaluations.Add(new StudentEvaluation
            {
                Booking = booking,
                ModeRating = date >= start && date < end ? (byte)5 : (byte)1,
                PlatformRating = 5
            });
            context.TutorModuleChangeRequests.Add(new TutorModuleChangeRequest
            {
                TutorId = 1, ProgrammeModuleId = 1, Status = TutorAccountRequestStatus.Pending,
                SubmittedAt = date.UtcDateTime
            });
        }
        await context.SaveChangesAsync();
        var page = new ProfileModel(context)
        {
            Period = "custom", StartDate = new DateOnly(2026, 9, 10), EndDate = new DateOnly(2026, 9, 11), SessionPage = 99
        };
        await page.OnGetAsync(1, CancellationToken.None);
        Assert.Null(page.DateFilterError);
        Assert.Equal(2, page.CompletedSessions);
        Assert.Equal(0, page.PendingStudentReviews);
        Assert.Equal(2, page.ModuleChangeRequests);
        Assert.Equal(2, page.ReviewCount);
        Assert.Equal(5d, page.AverageRating);
        Assert.Equal(2, Assert.Single(page.TopModules).Count);
        Assert.Equal(2, page.RecentSessions.Count);
        Assert.Equal(1, page.SessionPage);

        page.EndDate = page.StartDate;
        await page.OnGetAsync(1, CancellationToken.None);
        Assert.Equal(1, page.CompletedSessions);
        Assert.Single(page.RecentSessions);
        Assert.Equal(1, page.ReviewCount);
        Assert.Equal(5d, page.AverageRating);
        page.StartDate = new DateOnly(2020, 1, 1);
        page.EndDate = page.StartDate;
        await page.OnGetAsync(1, CancellationToken.None);
        Assert.Equal(0, page.CompletedSessions);
        Assert.Empty(page.RecentSessions);
        Assert.Empty(page.TopModules);
        Assert.Null(page.AverageRating);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("2026-09-12", "2026-09-10")]
    [InlineData("2026-09-12", "9999-12-31")]
    public async Task InvalidCustomDatesShowValidationWithoutThrowing(string? start, string? end)
    {
        await using var context = CreateContext();
        await SeedTutors(context);
        var page = new ProfileModel(context)
        {
            Period = "custom", StartDate = start is null ? null : DateOnly.Parse(start),
            EndDate = end is null ? null : DateOnly.Parse(end)
        };
        await page.OnGetAsync(1, CancellationToken.None);
        Assert.NotNull(page.DateFilterError);
    }

    [Fact]
    public async Task TutorListExcludesUnplacedInactiveAndUnapprovedTutorsBeforeCountingAndPaging()
    {
        await using var context = CreateContext();
        await SeedTutors(context);
        var tutors = await context.Tutors.OrderBy(tutor => tutor.TutorId).ToListAsync();
        tutors[0].IsActive = false;
        tutors[1].ApplicationStage = TutorApplicationStage.Interview;
        tutors[2].Status = TutorStatus.Pending;
        tutors[3].Status = TutorStatus.Rejected;
        tutors[4].Status = TutorStatus.Suspended;
        await context.SaveChangesAsync();

        var page = new IndexModel(context);
        await page.OnGetAsync(CancellationToken.None);
        Assert.Equal(13, page.TotalTutors);
        Assert.Equal(2, page.TotalPages);
        Assert.Equal(Enumerable.Range(6, 8), page.Tutors.Select(tutor => tutor.TutorId));
        page.TutorPage = 2;
        await page.OnGetAsync(CancellationToken.None);
        Assert.Equal(Enumerable.Range(14, 5), page.Tutors.Select(tutor => tutor.TutorId));

        page.SearchName = "Tutor 01";
        await page.OnGetAsync(CancellationToken.None);
        Assert.Empty(page.Tutors);
        Assert.Equal(0, page.TotalTutors);
        Assert.Equal(1, page.TutorPage);
    }

    [Fact]
    public async Task PaginationReturnsEightRowsWithoutOverlapAndClampsInvalidPages()
    {
        await using var context = CreateContext();
        await SeedTutors(context);
        var page = new IndexModel(context);
        await page.OnGetAsync(CancellationToken.None);
        Assert.Equal(8, page.Tutors.Count);
        Assert.Equal(18, page.TotalTutors);
        Assert.Equal(3, page.TotalPages);
        var firstIds = page.Tutors.Select(tutor => tutor.TutorId).ToArray();
        page.TutorPage = 2;
        await page.OnGetAsync(CancellationToken.None);
        Assert.Equal(8, page.Tutors.Count);
        Assert.DoesNotContain(page.Tutors, tutor => firstIds.Contains(tutor.TutorId));
        page.TutorPage = int.MaxValue;
        await page.OnGetAsync(CancellationToken.None);
        Assert.Equal(3, page.TutorPage);
        Assert.Equal(2, page.Tutors.Count);
        page.TutorPage = -5;
        await page.OnGetAsync(CancellationToken.None);
        Assert.Equal(1, page.TutorPage);
    }

    [Fact]
    public async Task TutorDirectoryExcludesRejectedApplications()
    {
        await using var context = CreateContext();
        await SeedTutors(context);
        var rejectedApplicant = new Tutor
        {
            TutorId = 19,
            BcUser = new BcUser
            {
                BcUserId = 19,
                PersonnelNumber = "S19",
                DisplayName = "Rejected Applicant"
            },
            ProgrammeId = 1,
            YearOfStudy = 2,
            ReasonForTutoring = "Reason",
            TeachingStyle = "Style",
            PreviousTutoringExperience = "Experience",
            CampusOfStudy = "Pretoria",
            DemonstrationVideoUrl = "",
            Status = TutorStatus.Rejected
        };
        context.Tutors.Add(rejectedApplicant);
        await context.SaveChangesAsync();

        var page = new IndexModel(context) { SearchName = "Rejected Applicant" };
        await page.OnGetAsync(CancellationToken.None);

        Assert.Empty(page.Tutors);
        Assert.Equal(0, page.TotalTutors);
    }

    [Theory]
    [InlineData("PRG")]
    [InlineData("Programming")]
    public async Task CombinedFiltersApplyBeforePaginationAndSupportMultipleYears(string module)
    {
        await using var context = CreateContext();
        await SeedTutors(context);
        var page = new IndexModel(context)
        {
            SearchName = " Tutor ", SearchModule = module,
            SearchCourse = " Computing ", Years = [2, 3], TutorPage = 2
        };
        await page.OnGetAsync(CancellationToken.None);
        Assert.Equal(9, page.TotalTutors);
        Assert.Single(page.Tutors);
        Assert.All(page.Tutors, tutor => Assert.Contains(tutor.YearOfStudy, new[] { 2, 3 }));
        page.SearchName = "No matching tutor";
        await page.OnGetAsync(CancellationToken.None);
        Assert.Empty(page.Tutors);
        Assert.Equal(0, page.TotalTutors);
        Assert.Equal(1, page.TutorPage);
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task SeedTutors(ApplicationDbContext context)
    {
        var programme = new ProgrammeOfStudy { Id = 1, Name = "Computing" };
        var module = new ProgrammeModule
        {
            ProgrammeModuleId = 1, Programme = programme,
            ModuleCode = "PRG101", ModuleName = "Programming"
        };
        for (int id = 1; id <= 18; id++)
        {
            context.Tutors.Add(new Tutor
            {
                TutorId = id,
                BcUser = new BcUser { BcUserId = id, PersonnelNumber = $"S{id}", DisplayName = $"Tutor {id:00}" },
                Programme = programme, YearOfStudy = 2 + (id % 3),
                ReasonForTutoring = "Reason", TeachingStyle = "Style",
                PreviousTutoringExperience = "Experience", CampusOfStudy = "Pretoria",
                DemonstrationVideoUrl = "", Status = TutorStatus.Approved,
                ApplicationStage = TutorApplicationStage.Placement, IsActive = true,
                TutorCourseModules = id <= 13
                    ? [new TutorCourseModule { ProgrammeModule = module }] : []
            });
        }
        await context.SaveChangesAsync();
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "BC_CampusLearn.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public bool IsAuthenticated => true;

        public CurrentUser GetRequiredUser() =>
            new(1, "A1", "Administrator", null, BcUserRole.Admin);
    }

    private static void SetPageContext(SessionDetailsModel page)
    {
        var httpContext = new DefaultHttpContext();
        page.PageContext = new PageContext
        {
            HttpContext = httpContext,
            ViewData = new ViewDataDictionary(
                new EmptyModelMetadataProvider(), new ModelStateDictionary())
        };
        page.TempData = new TempDataDictionary(
            httpContext, new TestTempDataProvider());
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) =>
            new Dictionary<string, object>();

        public void SaveTempData(
            HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class TestTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
