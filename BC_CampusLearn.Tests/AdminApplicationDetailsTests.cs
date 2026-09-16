using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Pages.Administrator.Admin;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace BC_CampusLearn.Tests;

public class AdminApplicationDetailsTests
{
    [Fact]
    public async Task DetailsLoadsTheSelectedStudentsSubmittedApplication()
    {
        await using ApplicationDbContext context = CreateContext();
        Tutor application = CreateApplication();
        context.Tutors.Add(application);
        await context.SaveChangesAsync();

        var page = new ApplicationDetailsModel(
            context,
            new TestWebHostEnvironment());

        IActionResult result = await page.OnGetAsync(
            application.TutorId,
            CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Student Example", page.DisplayName);
        Assert.Equal("Why I want to tutor", page.Application.ReasonForTutoring);
        Assert.Equal("PRG101", page.Application.TutorCourseModules
            .Single().ProgrammeModule.ModuleCode);
        Assert.Equal("transcript.pdf", page.Application.TutorDocuments
            .Single().OriginalFileName);
    }

    [Fact]
    public async Task DetailsReturnsNotFoundForUnknownApplication()
    {
        await using ApplicationDbContext context = CreateContext();
        var page = new ApplicationDetailsModel(
            context,
            new TestWebHostEnvironment());

        IActionResult result = await page.OnGetAsync(
            int.MaxValue,
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ApprovingApplicationMovesCandidateFromApplicationsToShortlist()
    {
        await using ApplicationDbContext context = CreateContext();
        Tutor application = CreateApplication();
        context.Tutors.Add(application);
        context.TutorApplicationSettings.Add(new TutorApplicationSettings
        {
            IsOpen = true,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var detailsPage = new ApplicationDetailsModel(
            context,
            new TestWebHostEnvironment())
        {
            ReviewReason = "Strong application and suitable module knowledge."
        };

        IActionResult result = await detailsPage.OnPostShortlistAsync(
            application.TutorId,
            CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Administrator/Admin/Applications", redirect.PageName);
        Assert.Equal("shortlist", redirect.RouteValues?["Stage"]);

        var applicationsPage = new ApplicationsModel(context)
        {
            Stage = "applications"
        };
        await applicationsPage.OnGetAsync(CancellationToken.None);
        Assert.Empty(applicationsPage.Candidates);
        Assert.Equal(0, applicationsPage.ApplicationCount);

        var shortlistPage = new ApplicationsModel(context)
        {
            Stage = "shortlist"
        };
        await shortlistPage.OnGetAsync(CancellationToken.None);
        ApplicationsModel.ApplicationCandidate shortlisted =
            Assert.Single(shortlistPage.Candidates);
        Assert.Equal(application.TutorId, shortlisted.TutorId);
        Assert.Equal(1, shortlistPage.ShortlistCount);
    }

    [Fact]
    public async Task RejectingApplicationDeletesTutorAndRedirectsToApplications()
    {
        await using ApplicationDbContext context = CreateContext();
        Tutor application = CreateApplication();
        context.Tutors.Add(application);
        await context.SaveChangesAsync();

        var detailsPage = new ApplicationDetailsModel(
            context,
            new TestWebHostEnvironment());
        detailsPage.ModelState.AddModelError(
            "Stage",
            "An unrelated binding error must not block rejection.");

        IActionResult result = await detailsPage.OnPostRejectAsync(
            application.TutorId,
            CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Administrator/Admin/Applications", redirect.PageName);
        Assert.Equal("applications", redirect.RouteValues?["Stage"]);
        Assert.False(redirect.RouteValues?.ContainsKey("Search"));
        Assert.True(detailsPage.ShowReviewResultModal);
        Assert.Null(await context.Tutors.FindAsync(application.TutorId));
        Assert.Empty(await context.TutorCourseModules.ToListAsync());
        Assert.Empty(await context.TutorDocuments.ToListAsync());
        Assert.NotNull(await context.BcUsers.FindAsync(application.BcUserId));

        var applicationsPage = new ApplicationsModel(context)
        {
            Stage = "applications"
        };
        await applicationsPage.OnGetAsync(CancellationToken.None);
        Assert.Empty(applicationsPage.Candidates);
    }

    [Fact]
    public async Task DocumentDownloadCannotAccessAnotherStudentsDocument()
    {
        await using ApplicationDbContext context = CreateContext();
        Tutor firstApplication = CreateApplication();
        Tutor secondApplication = CreateApplication();
        secondApplication.BcUser.PersonnelNumber = "600002";
        context.Tutors.AddRange(firstApplication, secondApplication);
        await context.SaveChangesAsync();

        TutorDocument secondDocument = secondApplication.TutorDocuments.Single();
        var page = new ApplicationDetailsModel(
            context,
            new TestWebHostEnvironment());

        IActionResult result = await page.OnGetDocumentAsync(
            firstApplication.TutorId,
            secondDocument.TutorDocumentId,
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Tutor CreateApplication()
    {
        var programme = new ProgrammeOfStudy
        {
            Name = "Bachelor of Computing"
        };
        var module = new ProgrammeModule
        {
            Programme = programme,
            ModuleCode = "PRG101",
            ModuleName = "Programming",
            YearOfStudy = 1
        };

        return new Tutor
        {
            BcUser = new BcUser
            {
                PersonnelNumber = "600001",
                DisplayName = "Student Example",
                Email = "student@example.test"
            },
            Programme = programme,
            OverallAverage = 78,
            YearOfStudy = 2,
            PhoneNumber = "0123456789",
            ReasonForTutoring = "Why I want to tutor",
            TeachingStyle = "Patient and practical",
            PreviousTutoringExperience = "Peer support",
            CampusOfStudy = "Pretoria Campus",
            DemonstrationVideoUrl = "https://example.test/video",
            PreferredTutoringMode = PreferredTutoringMode.Both,
            Status = TutorStatus.Pending,
            ApplicationStage = TutorApplicationStage.Submitted,
            SubmittedAt = DateTime.UtcNow,
            TutorCourseModules =
            [
                new TutorCourseModule
                {
                    ProgrammeModule = module
                }
            ],
            TutorDocuments =
            [
                new TutorDocument
                {
                    DocumentType = TutorDocumentType.AcademicTranscript,
                    FilePath = "App_Data/tutor-documents/test/transcript.pdf",
                    OriginalFileName = "transcript.pdf",
                    UploadedAt = DateTime.UtcNow
                }
            ]
        };
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "BC_CampusLearn.Tests";
        public IFileProvider WebRootFileProvider { get; set; }
            = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; }
            = new NullFileProvider();
    }
}
