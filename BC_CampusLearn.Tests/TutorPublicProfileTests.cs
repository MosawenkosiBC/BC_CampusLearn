using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using BC_CampusLearn.Pages.Tutors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BC_CampusLearn.Tests;

public class TutorPublicProfileTests
{
    [Fact]
    public async Task PublicProfileIncludesTutorsAccountEmail()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new ApplicationDbContext(options);
        var user = new BcUser
        {
            BcUserId = 1,
            PersonnelNumber = "600001",
            DisplayName = "Lebo Nkosi",
            Email = "600001@student.belgiumcampus.ac.za"
        };
        context.Tutors.Add(new Tutor
        {
            TutorId = 1,
            BcUser = user,
            Programme = new ProgrammeOfStudy
            {
                Id = 1,
                Name = "Bachelor of Computing"
            },
            OverallAverage = 75,
            YearOfStudy = 2,
            ReasonForTutoring = "Reason",
            TeachingStyle = "Teaching style",
            PreviousTutoringExperience = "Experience",
            PreferredTutoringMode = PreferredTutoringMode.Both,
            CampusOfStudy = "Pretoria Campus",
            DemonstrationVideoUrl = "https://example.test/video",
            Status = TutorStatus.Approved,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var page = new PublicProfileModel(
            context,
            new TestCurrentUserService(new CurrentUser(
                user.BcUserId,
                user.PersonnelNumber,
                user.DisplayName,
                user.Email,
                BcUserRole.Tutor)));

        var result = await page.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(user.Email, page.EmailAddress);
    }

    [Fact]
    public async Task UpdatingPublicProfileCreatesDescriptiveNotification()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new ApplicationDbContext(options);
        var user = new BcUser
        {
            BcUserId = 1,
            PersonnelNumber = "600001",
            DisplayName = "Lebo Nkosi",
            Email = "600001@student.belgiumcampus.ac.za"
        };
        context.Tutors.Add(new Tutor
        {
            TutorId = 1,
            BcUser = user,
            Programme = new ProgrammeOfStudy
            {
                Id = 1,
                Name = "Bachelor of Computing"
            },
            OverallAverage = 75,
            YearOfStudy = 2,
            ReasonForTutoring = "Reason",
            TeachingStyle = "Teaching style",
            PreviousTutoringExperience = "Experience",
            PreferredTutoringMode = PreferredTutoringMode.Online,
            CampusOfStudy = "Pretoria Campus",
            DemonstrationVideoUrl = "https://example.test/video",
            Biography = "Original biography",
            Status = TutorStatus.Approved,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var page = new PublicProfileModel(
            context,
            new TestCurrentUserService(new CurrentUser(
                user.BcUserId,
                user.PersonnelNumber,
                user.DisplayName,
                user.Email,
                BcUserRole.Tutor)))
        {
            Input = new TutorPublicProfileInput
            {
                Biography = "I make difficult concepts easy to understand.",
                PreferredTutoringMode = PreferredTutoringMode.Both,
                GitHubUrl = "https://github.com/lebo"
            }
        };
        SetPageContext(page);

        await page.OnPostAsync(CancellationToken.None);

        UserNotification notification = await context.UserNotifications
            .SingleAsync();
        Assert.Equal(user.BcUserId, notification.RecipientBcUserId);
        Assert.Equal("Public profile updated", notification.Title);
        Assert.Contains("biography", notification.Message);
        Assert.Contains("tutoring preference", notification.Message);
        Assert.Contains("GitHub link", notification.Message);
        Assert.Equal("/Tutors/PublicProfile", notification.LinkUrl);
    }

    private static void SetPageContext(PageModel page)
    {
        var httpContext = new DefaultHttpContext();
        page.PageContext = new PageContext { HttpContext = httpContext };
        page.TempData = new TempDataDictionary(
            httpContext,
            new TestTempDataProvider());
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) =>
            new Dictionary<string, object>();

        public void SaveTempData(
            HttpContext context,
            IDictionary<string, object> values) { }
    }

    private sealed class TestCurrentUserService(CurrentUser user)
        : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public CurrentUser GetRequiredUser() => user;
    }
}
