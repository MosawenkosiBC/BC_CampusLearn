using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Pages.Administrator.Tutors;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BC_CampusLearn.Tests;

public class AdminTutorsTests
{
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
        Assert.Equal(5, page.RecentSessions.Count);
        Assert.All(page.RecentSessions, item => Assert.Contains(item.Status, new[] { BookingStatus.Completed, BookingStatus.Cancelled }));
        var details = new SessionDetailsModel(context);
        Assert.IsType<Microsoft.AspNetCore.Mvc.RazorPages.PageResult>(await details.OnGetAsync(existingBooking.BookingId, CancellationToken.None));
        Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundResult>(await details.OnGetAsync(hiddenBooking.BookingId, CancellationToken.None));
        Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundResult>(await details.OnGetAsync(int.MaxValue, CancellationToken.None));
        Assert.Equal(8, page.TotalSessionPages);
        var firstPageIds = page.RecentSessions.Select(item => item.BookingId).ToArray();
        page.SessionPage = 2;
        await page.OnGetAsync(1, CancellationToken.None);
        Assert.Equal(5, page.RecentSessions.Count);
        Assert.DoesNotContain(page.RecentSessions, item => firstPageIds.Contains(item.BookingId));
        page.SessionPage = int.MaxValue;
        await page.OnGetAsync(1, CancellationToken.None);
        Assert.Equal(8, page.SessionPage);
        Assert.Equal(4, page.RecentSessions.Count);
        Assert.Null(page.AverageRating);
        await page.OnGetAsync(18, CancellationToken.None);
        Assert.Empty(page.TopModules);
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
                TutorCourseModules = id <= 13
                    ? [new TutorCourseModule { ProgrammeModule = module }] : []
            });
        }
        await context.SaveChangesAsync();
    }
}
