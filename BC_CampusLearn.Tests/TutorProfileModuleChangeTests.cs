using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using BC_CampusLearn.Pages.Tutors;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace BC_CampusLearn.Tests;

public class TutorProfileModuleChangeTests
{
    [Fact]
    public async Task RequestModuleChangeCreatesOneRequestForEachSelectedModule()
    {
        await using ApplicationDbContext context = await CreateContextAsync();
        ProfileModel page = SetupPage(context);
        page.ModuleRequestInput = new TutorModuleChangeRequestInput
        {
            RequestType = TutorModuleChangeRequestType.Add,
            ProgrammeModuleIds = [1, 2, 2],
            Reason = "I can confidently tutor both modules."
        };

        IActionResult result = await page.OnPostRequestModuleChangeAsync(default);

        Assert.IsType<RedirectToPageResult>(result);
        TutorModuleChangeRequest[] requests = await context.TutorModuleChangeRequests
            .OrderBy(request => request.ProgrammeModuleId)
            .ToArrayAsync();
        Assert.Equal([1, 2], requests.Select(request => request.ProgrammeModuleId));
        Assert.All(requests, request =>
        {
            Assert.Equal(TutorModuleChangeRequestType.Add, request.RequestType);
            Assert.Equal(TutorAccountRequestStatus.Pending, request.Status);
        });
    }

    [Fact]
    public async Task RequestModuleChangeRejectsEntireSelectionWhenAnyModuleIsOutsideProgramme()
    {
        await using ApplicationDbContext context = await CreateContextAsync();
        context.ProgrammesOfStudy.Add(new ProgrammeOfStudy { Id = 2, Name = "Business" });
        context.ProgrammeModules.Add(new ProgrammeModule
        {
            ProgrammeModuleId = 3,
            ProgrammeId = 2,
            ModuleCode = "BUS101",
            ModuleName = "Business",
            YearOfStudy = 1
        });
        await context.SaveChangesAsync();
        ProfileModel page = SetupPage(context);
        page.ModuleRequestInput = new TutorModuleChangeRequestInput
        {
            RequestType = TutorModuleChangeRequestType.Add,
            ProgrammeModuleIds = [1, 3],
            Reason = "I would like to tutor these modules."
        };

        IActionResult result = await page.OnPostRequestModuleChangeAsync(default);

        Assert.IsType<PageResult>(result);
        Assert.False(page.ModelState.IsValid);
        Assert.Empty(await context.TutorModuleChangeRequests.ToListAsync());
    }

    private static ProfileModel SetupPage(ApplicationDbContext context)
    {
        var currentUser = new CurrentUser(
            1,
            "T1",
            "Test Tutor",
            "tutor@example.com",
            BcUserRole.Tutor);
        var page = new ProfileModel(
            context,
            new TestCurrentUserService(currentUser),
            new TestWebHostEnvironment());
        var httpContext = new DefaultHttpContext();
        page.PageContext = new PageContext { HttpContext = httpContext };
        page.TempData = new TempDataDictionary(httpContext, new MemoryTempDataProvider());
        return page;
    }

    private static async Task<ApplicationDbContext> CreateContextAsync()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new ApplicationDbContext(options);
        context.ProgrammesOfStudy.Add(new ProgrammeOfStudy { Id = 1, Name = "Computing" });
        context.ProgrammeModules.AddRange(
            new ProgrammeModule
            {
                ProgrammeModuleId = 1,
                ProgrammeId = 1,
                ModuleCode = "PRG101",
                ModuleName = "Programming",
                YearOfStudy = 1
            },
            new ProgrammeModule
            {
                ProgrammeModuleId = 2,
                ProgrammeId = 1,
                ModuleCode = "DBS201",
                ModuleName = "Databases",
                YearOfStudy = 2
            });
        context.Tutors.Add(new Tutor
        {
            TutorId = 1,
            BcUserId = 1,
            ProgrammeId = 1,
            BcUser = new BcUser
            {
                BcUserId = 1,
                PersonnelNumber = "T1",
                DisplayName = "Test Tutor",
                Email = "tutor@example.com"
            },
            Status = TutorStatus.Approved,
            ApplicationStage = TutorApplicationStage.Placement,
            IsActive = true,
            YearOfStudy = 3,
            ReasonForTutoring = "Reason",
            TeachingStyle = "Style",
            PreviousTutoringExperience = "Experience",
            CampusOfStudy = "Pretoria",
            DemonstrationVideoUrl = ""
        });
        await context.SaveChangesAsync();
        return context;
    }

    private sealed class TestCurrentUserService(CurrentUser user) : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public CurrentUser GetRequiredUser() => user;
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

    private sealed class MemoryTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) =>
            new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
