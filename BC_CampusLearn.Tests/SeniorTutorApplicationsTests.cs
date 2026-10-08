using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using BC_CampusLearn.Pages.Tutors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BC_CampusLearn.Tests;

public class SeniorTutorApplicationsTests
{
    [Fact]
    public async Task ValidApplicationIsStoredAsPending()
    {
        await using ApplicationDbContext context = await CreateContextAsync();
        SeniorTutorApplicationsModel page = CreatePage(context);
        page.Input = new SeniorTutorApplicationInput
        {
            AcademicAverage = "78.5",
            BestDescription = "Good leadership skills",
            SuitabilityReason = "I have supported other tutors and can coordinate the programme effectively."
        };

        IActionResult result = await page.OnPostApplyAsync(default);

        Assert.IsType<RedirectToPageResult>(result);
        SeniorTutorApplication application = await context.SeniorTutorApplications.SingleAsync();
        Assert.Equal(78.5m, application.AcademicAverage);
        Assert.Equal("Good leadership skills", application.BestDescription);
        Assert.Equal(TutorAccountRequestStatus.Pending, application.Status);
    }

    [Theory]
    [InlineData("71.99")]
    [InlineData("not a number")]
    [InlineData("")]
    public async Task InvalidAcademicAverageDoesNotCreateApplication(string average)
    {
        await using ApplicationDbContext context = await CreateContextAsync();
        SeniorTutorApplicationsModel page = CreatePage(context);
        page.Input = new SeniorTutorApplicationInput
        {
            AcademicAverage = average,
            BestDescription = "Motivated",
            SuitabilityReason = "I am ready to take on the additional responsibility."
        };

        IActionResult result = await page.OnPostApplyAsync(default);

        Assert.IsType<PageResult>(result);
        Assert.False(page.ModelState.IsValid);
        Assert.Empty(await context.SeniorTutorApplications.ToListAsync());
        Assert.True(page.OpenApplicationModal);
    }

    [Fact]
    public async Task TutorCannotSubmitWhileAnApplicationIsPending()
    {
        await using ApplicationDbContext context = await CreateContextAsync();
        context.SeniorTutorApplications.Add(new SeniorTutorApplication
        {
            TutorId = 1,
            AcademicAverage = 75,
            BestDescription = "Professional",
            SuitabilityReason = "Existing application",
            Status = TutorAccountRequestStatus.Pending,
            SubmittedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        SeniorTutorApplicationsModel page = CreatePage(context);
        page.Input = new SeniorTutorApplicationInput
        {
            AcademicAverage = "80",
            BestDescription = "Self-driven",
            SuitabilityReason = "Duplicate application"
        };

        IActionResult result = await page.OnPostApplyAsync(default);

        Assert.IsType<PageResult>(result);
        Assert.Single(await context.SeniorTutorApplications.ToListAsync());
        Assert.False(page.ModelState.IsValid);
    }

    [Fact]
    public async Task AdministratorTutorPageLoadsSeniorTutorApplicationsTab()
    {
        await using ApplicationDbContext context = await CreateContextAsync();
        context.SeniorTutorApplications.AddRange(
            new SeniorTutorApplication
            {
                TutorId = 1,
                AcademicAverage = 82,
                BestDescription = "Good leadership skills",
                SuitabilityReason = "Pending application",
                Status = TutorAccountRequestStatus.Pending,
                SubmittedAt = DateTime.UtcNow
            },
            new SeniorTutorApplication
            {
                TutorId = 1,
                AcademicAverage = 76,
                BestDescription = "Professional",
                SuitabilityReason = "Reviewed application",
                Status = TutorAccountRequestStatus.Approved,
                SubmittedAt = DateTime.UtcNow.AddDays(-1)
            });
        await context.SaveChangesAsync();
        var page = new global::BC_CampusLearn.Pages.Administrator.Tutors.IndexModel(
            context,
            null!)
        {
            Tab = "senior-applications"
        };

        await page.OnGetAsync(default);

        Assert.Equal("senior-applications", page.Tab);
        Assert.Equal(2, page.TotalApplications);
        Assert.Equal(1, page.PendingApplications);
        Assert.Equal(2, page.SeniorTutorApplications.Count);
        Assert.Equal(
            TutorAccountRequestStatus.Pending,
            page.SeniorTutorApplications[0].Status);
    }

    [Fact]
    public async Task AdministratorCanAcceptSeniorTutorApplication()
    {
        await using ApplicationDbContext context = await CreateContextAsync();
        var application = new SeniorTutorApplication
        {
            TutorId = 1,
            AcademicAverage = 80,
            BestDescription = "Good leadership skills",
            SuitabilityReason = "I can support and coordinate the tutor team.",
            Status = TutorAccountRequestStatus.Pending,
            SubmittedAt = DateTime.UtcNow
        };
        context.SeniorTutorApplications.Add(application);
        await context.SaveChangesAsync();
        var page = new global::BC_CampusLearn.Pages.Administrator.Tutors
            .SeniorTutorApplicationDetailsModel(context)
        {
            ReviewNote = "Strong tutoring record."
        };
        page.PageContext = new PageContext
        {
            HttpContext = new DefaultHttpContext()
        };

        IActionResult result = await page.OnPostReviewAsync(
            application.SeniorTutorApplicationId,
            approve: true,
            default);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(TutorAccountRequestStatus.Approved, application.Status);
        Assert.NotNull(application.ReviewedAt);
        UserNotification notification = await context.UserNotifications.SingleAsync();
        Assert.Equal(1, notification.RecipientBcUserId);
        Assert.Contains("accepted", notification.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static SeniorTutorApplicationsModel CreatePage(ApplicationDbContext context)
    {
        var page = new SeniorTutorApplicationsModel(
            context,
            new TestCurrentUserService(new CurrentUser(
                1,
                "600001",
                "Test Tutor",
                "tutor@example.com",
                BcUserRole.Tutor)));
        var httpContext = new DefaultHttpContext();
        page.PageContext = new PageContext { HttpContext = httpContext };
        page.TempData = new TempDataDictionary(
            httpContext,
            new TestTempDataProvider());
        return page;
    }

    private static async Task<ApplicationDbContext> CreateContextAsync()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new ApplicationDbContext(options);
        context.Tutors.Add(new Tutor
        {
            TutorId = 1,
            BcUserId = 1,
            ProgrammeId = 1,
            BcUser = new BcUser
            {
                BcUserId = 1,
                PersonnelNumber = "600001",
                DisplayName = "Test Tutor",
                Email = "tutor@example.com"
            },
            Programme = new ProgrammeOfStudy
            {
                Id = 1,
                Name = "Bachelor of Computing"
            },
            Status = TutorStatus.Approved,
            ApplicationStage = TutorApplicationStage.Placement,
            IsActive = true,
            OverallAverage = 78,
            YearOfStudy = 3,
            ReasonForTutoring = "Reason",
            TeachingStyle = "Style",
            PreviousTutoringExperience = "Experience",
            CampusOfStudy = "Pretoria Campus",
            DemonstrationVideoUrl = ""
        });
        await context.SaveChangesAsync();
        return context;
    }

    private sealed class TestCurrentUserService(CurrentUser user)
        : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public CurrentUser GetRequiredUser() => user;
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) =>
            new Dictionary<string, object>();

        public void SaveTempData(
            HttpContext context,
            IDictionary<string, object> values)
        {
        }
    }
}
