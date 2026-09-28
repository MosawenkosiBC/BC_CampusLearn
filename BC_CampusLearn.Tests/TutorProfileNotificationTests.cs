using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using BC_CampusLearn.Pages.Tutors;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace BC_CampusLearn.Tests;

public class TutorProfileNotificationTests
{
    [Fact]
    public async Task ModuleChangeRequestCreatesDescriptiveNotification()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new ApplicationDbContext(options);
        var programme = new ProgrammeOfStudy
        {
            Id = 1,
            Name = "Bachelor of Computing"
        };
        var module = new ProgrammeModule
        {
            ProgrammeModuleId = 10,
            Programme = programme,
            ModuleCode = "PRG201",
            ModuleName = "Programming 201",
            YearOfStudy = 2
        };
        var user = new BcUser
        {
            BcUserId = 1,
            PersonnelNumber = "600001",
            DisplayName = "Lebo Nkosi",
            Email = "lebo@example.test"
        };
        var tutor = new Tutor
        {
            TutorId = 1,
            BcUser = user,
            Programme = programme,
            OverallAverage = 75,
            YearOfStudy = 2,
            ReasonForTutoring = "Reason",
            TeachingStyle = "Teaching style",
            PreviousTutoringExperience = "Experience",
            PreferredTutoringMode = PreferredTutoringMode.Both,
            CampusOfStudy = "Pretoria Campus",
            DemonstrationVideoUrl = "https://example.test/video",
            Status = TutorStatus.Approved,
            ApplicationStage = TutorApplicationStage.Placement,
            IsActive = true
        };
        context.AddRange(programme, module, user, tutor);
        await context.SaveChangesAsync();

        var page = new ProfileModel(
            context,
            new TestCurrentUserService(new CurrentUser(
                user.BcUserId,
                user.PersonnelNumber,
                user.DisplayName,
                user.Email,
                BcUserRole.Tutor)),
            new TestWebHostEnvironment())
        {
            ModuleRequestInput = new TutorModuleChangeRequestInput
            {
                RequestType = TutorModuleChangeRequestType.Add,
                ProgrammeModuleId = module.ProgrammeModuleId,
                Reason = "I am qualified to tutor this module."
            }
        };
        SetPageContext(page);

        await page.OnPostRequestModuleChangeAsync(CancellationToken.None);

        Assert.Single(context.TutorModuleChangeRequests);
        UserNotification notification = await context.UserNotifications
            .SingleAsync();
        Assert.Equal(user.BcUserId, notification.RecipientBcUserId);
        Assert.Equal("Module change request submitted", notification.Title);
        Assert.Contains("add PRG201 (Programming 201)", notification.Message);
        Assert.Contains("waiting for administrator review", notification.Message);
        Assert.Equal("/Tutors/Profile", notification.LinkUrl);
    }

    private static void SetPageContext(PageModel page)
    {
        var httpContext = new DefaultHttpContext();
        page.PageContext = new PageContext { HttpContext = httpContext };
        page.TempData = new TempDataDictionary(
            httpContext,
            new TestTempDataProvider());
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
            IDictionary<string, object> values) { }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } =
            "BC_CampusLearn.Tests";
        public string EnvironmentName { get; set; } = "Testing";
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider WebRootFileProvider { get; set; } =
            new NullFileProvider();
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }
}
