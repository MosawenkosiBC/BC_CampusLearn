using BC_CampusLearn.Authentication;
using System.Security.Claims;
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
    public async Task ValidSeniorTutorApplicationIsStoredAsPending()
    {
        await using ApplicationDbContext context = await CreateContextAsync();
        SeniorTutorApplicationsModel page = CreatePage(context);
        IActionResult result = await page.OnPostApplySeniorAsync(
            "78.5",
            default,
            applyForHeadOfTutor: false);

        Assert.IsType<RedirectToPageResult>(result);
        SeniorTutorApplication application = await context.SeniorTutorApplications.SingleAsync();
        Assert.Equal(78.5m, application.AcademicAverage);
        Assert.False(application.IsHeadOfTutorApplication);
        Assert.Null(application.BestDescription);
        Assert.Equal(TutorAccountRequestStatus.Pending, application.Status);
    }

    [Theory]
    [InlineData("-0.01")]
    [InlineData("100.01")]
    [InlineData("not a number")]
    [InlineData("")]
    public async Task InvalidAcademicAverageDoesNotCreateApplication(string average)
    {
        await using ApplicationDbContext context = await CreateContextAsync();
        SeniorTutorApplicationsModel page = CreatePage(context);
        IActionResult result = await page.OnPostApplySeniorAsync(
            average,
            default);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Empty(await context.SeniorTutorApplications.ToListAsync());
    }

    [Fact]
    public async Task ClosedSeniorTutorApplicationsCannotBeSubmitted()
    {
        await using ApplicationDbContext context = await CreateContextAsync();
        TutorApplicationSettings settings = await context
            .TutorApplicationSettings.SingleAsync();
        settings.SeniorTutorApplicationsOpen = false;
        await context.SaveChangesAsync();
        SeniorTutorApplicationsModel page = CreatePage(context);
        IActionResult result = await page.OnPostApplySeniorAsync(
            "80",
            default);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Empty(await context.SeniorTutorApplications.ToListAsync());
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
        IActionResult result = await page.OnPostApplySeniorAsync(
            "80",
            default);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Single(await context.SeniorTutorApplications.ToListAsync());
    }

    [Fact]
    public async Task HeadOfTutorsCanApplyToReturnAsSeniorTutorWithoutHotRedirect()
    {
        await using ApplicationDbContext context = await CreateContextAsync();
        BcUser user = await context.BcUsers.SingleAsync();
        user.Role = BcUserRole.HeadOfTutors;
        await context.SaveChangesAsync();
        SeniorTutorApplicationsModel page = CreatePage(
            context,
            BcUserRole.HeadOfTutors);
        IActionResult result = await page.OnPostApplySeniorAsync(
            "80",
            default);

        RedirectToPageResult redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Tutors/TutorDashboard", redirect.PageName);
        Assert.Null(redirect.RouteValues?["applicationId"]);
        SeniorTutorApplication application =
            await context.SeniorTutorApplications.SingleAsync();
        Assert.False(application.IsHeadOfTutorApplication);

        var reviewPage = new global::BC_CampusLearn.Pages.Administrator.Tutors
            .SeniorTutorApplicationDetailsModel(context);
        reviewPage.PageContext = new PageContext
        {
            HttpContext = new DefaultHttpContext()
        };
        reviewPage.TempData = new TempDataDictionary(reviewPage.HttpContext, new TestTempDataProvider());
        await reviewPage.OnPostReviewAsync(
            application.SeniorTutorApplicationId,
            approve: true,
            default);

        Assert.Equal(
            BcUserRole.SeniorTutor,
            (await context.BcUsers.SingleAsync()).Role);
    }

    [Fact]
    public async Task DemotedTutorDoesNotSeeStaleApprovedApplication()
    {
        await using ApplicationDbContext context = await CreateContextAsync();
        context.SeniorTutorApplications.Add(new SeniorTutorApplication
        {
            TutorId = 1,
            AcademicAverage = 80,
            BestDescription = "Good leadership skills",
            SuitabilityReason = "Previously approved application",
            Status = TutorAccountRequestStatus.Approved,
            IsArchived = true,
            SubmittedAt = DateTime.UtcNow.AddDays(-1)
        });
        await context.SaveChangesAsync();
        SeniorTutorApplicationsModel page = CreatePage(context);

        IActionResult result = await page.OnGetAsync(default);

        Assert.IsType<PageResult>(result);
        Assert.Null(page.LatestApplication);
        Assert.False(page.IsHeadOfTutors);
    }

    [Fact]
    public async Task ActiveTutorUpdatesPreserveHeadOfTutorsRole()
    {
        await using ApplicationDbContext context = await CreateContextAsync();
        BcUser user = await context.BcUsers.SingleAsync();
        Tutor tutor = await context.Tutors.SingleAsync();
        user.Role = BcUserRole.HeadOfTutors;
        tutor.OverallAverage = 81;

        await context.SaveChangesAsync();

        Assert.Equal(BcUserRole.HeadOfTutors, user.Role);
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
                IsHeadOfTutorApplication = true,
                BestDescription = "Professional",
                SuitabilityReason = "Reviewed application",
                Status = TutorAccountRequestStatus.Approved,
                IsArchived = true,
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
        Assert.Equal(1, page.TotalApplications);
        Assert.Equal(1, page.PendingApplications);
        Assert.Single(page.SeniorTutorApplications);
        Assert.Equal(
            TutorAccountRequestStatus.Pending,
            page.SeniorTutorApplications[0].Status);

        BcUser user = await context.BcUsers.SingleAsync();
        user.Role = BcUserRole.HeadOfTutors;
        await context.SaveChangesAsync();
        page.ApplicationStatus = TutorAccountRequestStatus.Approved;
        await page.OnGetAsync(default);

        Assert.Equal(1, page.TotalApplications);
        Assert.Equal("all", page.ApplicationArchive);
        SeniorTutorApplication archived = Assert.Single(
            page.SeniorTutorApplications);
        Assert.True(archived.IsArchived);
        Assert.Equal(TutorAccountRequestStatus.Approved, archived.Status);
    }

    [Theory]
    [InlineData(true, BcUserRole.HeadOfTutors)]
    [InlineData(false, BcUserRole.SeniorTutor)]
    public async Task SuperAdminCanChooseCombinedApplicationOutcome(bool approveHead, BcUserRole expectedRole)
    {
        await using ApplicationDbContext context = await CreateContextAsync();
        var application = new SeniorTutorApplication
        {
            TutorId = 1,
            AcademicAverage = 80,
            IsHeadOfTutorApplication = true,
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
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.Role, nameof(BcUserRole.SuperAdmin))], "Test"))
            }
        };

        page.TempData = new TempDataDictionary(page.HttpContext, new TestTempDataProvider());
        IActionResult result = await page.OnPostReviewAsync(
            application.SeniorTutorApplicationId,
            approve: true,
            default,
            approveSenior: true,
            approveHead: approveHead);

        RedirectToPageResult redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("Index", redirect.PageName);
        Assert.Equal("senior-applications", redirect.RouteValues?["Tab"]);
        Assert.Equal(TutorAccountRequestStatus.Approved, application.Status);
        Assert.True(application.IsArchived);
        Assert.Equal(
            expectedRole,
            (await context.BcUsers.SingleAsync()).Role);
        Assert.Equal(approveHead, application.HeadOfTutorApproved);
        Assert.Equal(80, (await context.Tutors.SingleAsync()).OverallAverage);
        Assert.NotNull(application.ReviewedAt);
        UserNotification notification = await context.UserNotifications.SingleAsync();
        Assert.Equal(1, notification.RecipientBcUserId);
        Assert.Contains("accepted", notification.Message, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("Congratulations!", notification.Message);
        if (!approveHead) Assert.Contains("Head of Tutor application was not accepted", notification.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectionPreservesTutorAndApplication(bool combined)
    {
        await using ApplicationDbContext context = await CreateContextAsync();
        var application = new SeniorTutorApplication
        {
            TutorId = 1, AcademicAverage = 80,
            IsHeadOfTutorApplication = combined,
            Status = TutorAccountRequestStatus.Pending, SubmittedAt = DateTime.UtcNow
        };
        context.SeniorTutorApplications.Add(application);
        await context.SaveChangesAsync();
        var page = ReviewPage(context, BcUserRole.SuperAdmin);
        page.ReviewNote = "Please build more leadership experience.";
        page.TempData = new TempDataDictionary(page.HttpContext, new TestTempDataProvider());
        await page.OnPostReviewAsync(application.SeniorTutorApplicationId, false, default);
        Assert.Equal(TutorAccountRequestStatus.Declined, application.Status);
        Assert.False(application.IsArchived);
        Assert.Single(await context.SeniorTutorApplications.ToListAsync());
        Tutor tutor = await context.Tutors.Include(item => item.BcUser).SingleAsync();
        Assert.True(tutor.IsActive);
        Assert.Equal(TutorStatus.Approved, tutor.Status);
        Assert.Equal(BcUserRole.Tutor, tutor.BcUser.Role);
        Assert.Equal(78m, tutor.OverallAverage);
        Assert.Equal(combined ? (bool?)false : null, application.HeadOfTutorApproved);
        Assert.Contains("remain part of our tutor programme", (await context.UserNotifications.SingleAsync()).Message);
        Assert.Equal(page.ReviewNote, application.ReviewNote);
        Assert.DoesNotContain(page.ReviewNote!, (await context.UserNotifications.SingleAsync()).Message);
    }

    [Theory]
    [InlineData(BcUserRole.Admin, true, true)]
    [InlineData(BcUserRole.SuperAdmin, false, false)]
    [InlineData(BcUserRole.SuperAdmin, false, true)]
    [InlineData(BcUserRole.SuperAdmin, null, null)]
    public async Task CombinedApprovalRequiresSuperAdminAndExplicitValidChoices(
        BcUserRole role, bool? senior, bool? head)
    {
        await using ApplicationDbContext context = await CreateContextAsync();
        var application = new SeniorTutorApplication
        {
            TutorId = 1, AcademicAverage = 80, IsHeadOfTutorApplication = true,
            Status = TutorAccountRequestStatus.Pending, SubmittedAt = DateTime.UtcNow
        };
        context.SeniorTutorApplications.Add(application);
        await context.SaveChangesAsync();
        var page = ReviewPage(context, role);
        page.TempData = new TempDataDictionary(page.HttpContext, new TestTempDataProvider());
        IActionResult result = await page.OnPostReviewAsync(
            application.SeniorTutorApplicationId, true, default, senior, head);
        if (role == BcUserRole.Admin) Assert.IsType<ForbidResult>(result);
        Assert.Equal(TutorAccountRequestStatus.Pending, application.Status);
        Assert.Null(application.HeadOfTutorApproved);
        Assert.Empty(await context.UserNotifications.ToListAsync());
        Assert.Equal(BcUserRole.Tutor, (await context.BcUsers.SingleAsync()).Role);
    }

    private static global::BC_CampusLearn.Pages.Administrator.Tutors.SeniorTutorApplicationDetailsModel
        ReviewPage(ApplicationDbContext context, BcUserRole role)
        => new(context)
        {
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.Role, role.ToString())], "Test"))
                }
            }
        };

    [Fact]
    public async Task ApprovedSeniorTutorApplicationUpdatesAverageAndAssignsSeniorTutorRole()
    {
        await using ApplicationDbContext context = await CreateContextAsync();
        var application = new SeniorTutorApplication
        {
            TutorId = 1,
            AcademicAverage = 68,
            IsHeadOfTutorApplication = false,
            Status = TutorAccountRequestStatus.Pending,
            SubmittedAt = DateTime.UtcNow
        };
        context.SeniorTutorApplications.Add(application);
        await context.SaveChangesAsync();
        Assert.Equal(78, (await context.Tutors.SingleAsync()).OverallAverage);

        var page = new global::BC_CampusLearn.Pages.Administrator.Tutors
            .SeniorTutorApplicationDetailsModel(context);
        page.PageContext = new PageContext
        {
            HttpContext = new DefaultHttpContext()
        };

        page.TempData = new TempDataDictionary(page.HttpContext, new TestTempDataProvider());
        await page.OnPostReviewAsync(
            application.SeniorTutorApplicationId,
            approve: true,
            default);

        Assert.Equal(68, (await context.Tutors.SingleAsync()).OverallAverage);
        Assert.Equal(
            BcUserRole.SeniorTutor,
            (await context.BcUsers.SingleAsync()).Role);
    }

    [Fact]
    public async Task AdditionalModulesAreStoredAndActivatedOnlyAfterApproval()
    {
        await using ApplicationDbContext context = await CreateContextAsync();
        context.ProgrammeModules.Add(new ProgrammeModule
        {
            ProgrammeModuleId = 20,
            ProgrammeId = 1,
            ModuleCode = "PRG202",
            ModuleName = "Advanced Programming",
            YearOfStudy = 2
        });
        await context.SaveChangesAsync();

        SeniorTutorApplicationsModel page = CreatePage(context);
        await page.OnPostApplySeniorAsync("70", default, [20]);

        SeniorTutorApplication application = await context
            .SeniorTutorApplications
            .Include(item => item.RequestedModules)
            .SingleAsync();
        Assert.Equal(20, Assert.Single(application.RequestedModules).ProgrammeModuleId);
        Assert.Empty(await context.TutorCourseModules.ToListAsync());

        var reviewPage = new global::BC_CampusLearn.Pages.Administrator.Tutors
            .SeniorTutorApplicationDetailsModel(context);
        reviewPage.PageContext = new PageContext
        {
            HttpContext = new DefaultHttpContext()
        };
        reviewPage.TempData = new TempDataDictionary(reviewPage.HttpContext, new TestTempDataProvider());
        await reviewPage.OnPostReviewAsync(
            application.SeniorTutorApplicationId,
            approve: true,
            default);

        TutorCourseModule assignment = await context.TutorCourseModules.SingleAsync();
        Assert.Equal(20, assignment.ProgrammeModuleId);
        Assert.True(assignment.IsActive);
    }

    [Fact]
    public async Task BelowThresholdApplicationDoesNotRequireHeadOfTutorAnswers()
    {
        await using ApplicationDbContext context = await CreateContextAsync();
        SeniorTutorApplicationsModel page = CreatePage(context);
        IActionResult result = await page.OnPostApplySeniorAsync(
            "68",
            default);

        Assert.IsType<RedirectToPageResult>(result);
        SeniorTutorApplication application =
            await context.SeniorTutorApplications.SingleAsync();
        Assert.False(application.IsHeadOfTutorApplication);
        Assert.Null(application.BestDescription);
        Assert.Null(application.SuitabilityReason);
        Assert.Equal(78, (await context.Tutors.SingleAsync()).OverallAverage);
    }

    [Fact]
    public async Task QualifiedSeniorTutorApplicationRedirectsToHeadOfTutorApplication()
    {
        await using ApplicationDbContext context = await CreateContextAsync();
        SeniorTutorApplicationsModel page = CreatePage(context);
        IActionResult result = await page.OnPostApplySeniorAsync(
            "72",
            default,
            applyForHeadOfTutor: true);

        RedirectToPageResult redirect = Assert.IsType<RedirectToPageResult>(result);
        SeniorTutorApplication application =
            await context.SeniorTutorApplications.SingleAsync();
        Assert.Equal(
            application.SeniorTutorApplicationId,
            redirect.RouteValues?["applicationId"]);
        Assert.False(application.IsHeadOfTutorApplication);
    }

    [Fact]
    public async Task QualifiedTutorMustChooseWhetherToContinueToHeadApplication()
    {
        await using ApplicationDbContext context = await CreateContextAsync();
        SeniorTutorApplicationsModel page = CreatePage(context);

        IActionResult result = await page.OnPostApplySeniorAsync(
            "75",
            default);

        RedirectToPageResult redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Tutors/TutorDashboard", redirect.PageName);
        Assert.Equal(true, redirect.RouteValues?["openSeniorApplication"]);
        Assert.Empty(await context.SeniorTutorApplications.ToListAsync());
    }

    [Fact]
    public async Task QualifiedApplicantCanAddHeadOfTutorApplication()
    {
        await using ApplicationDbContext context = await CreateContextAsync();
        var application = new SeniorTutorApplication
        {
            TutorId = 1,
            AcademicAverage = 78,
            Status = TutorAccountRequestStatus.Pending,
            SubmittedAt = DateTime.UtcNow
        };
        context.SeniorTutorApplications.Add(application);
        await context.SaveChangesAsync();
        SeniorTutorApplicationsModel page = CreatePage(context);
        page.Input = new SeniorTutorApplicationInput
        {
            BestDescription = "Good leadership skills",
            SuitabilityReason = "I can lead and support the tutor team."
        };

        IActionResult result = await page.OnPostApplyForHeadAsync(
            application.SeniorTutorApplicationId,
            default);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.True(application.IsHeadOfTutorApplication);
        Assert.Equal("Good leadership skills", application.BestDescription);
        Assert.Equal(
            "I can lead and support the tutor team.",
            application.SuitabilityReason);
    }

    private static SeniorTutorApplicationsModel CreatePage(
        ApplicationDbContext context,
        BcUserRole role = BcUserRole.Tutor)
    {
        var page = new SeniorTutorApplicationsModel(
            context,
            new TestCurrentUserService(new CurrentUser(
                1,
                "600001",
                "Test Tutor",
                "tutor@example.com",
                role)));
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
        context.TutorApplicationSettings.Add(new TutorApplicationSettings
        {
            SeniorTutorApplicationsOpen = true,
            UpdatedAt = DateTime.UtcNow
        });
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
                Email = "tutor@example.com",
                Role = BcUserRole.Tutor
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
