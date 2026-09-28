using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using AdminResourcesPage = BC_CampusLearn.Pages.Administrator.LearningResources.IndexModel;
using ResourceDetailsPage = BC_CampusLearn.Pages.LearningResources.DetailsModel;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace BC_CampusLearn.Tests;

public class AdminLearningResourcesTests
{
    [Fact]
    public async Task IndexShowsDraftAndPublishedResourcesAndAppliesFilters()
    {
        await using ApplicationDbContext context = CreateContext();
        SeedResources(context);
        await context.SaveChangesAsync();
        var page = new AdminResourcesPage(context, CreateEnvironment())
        {
            SearchModule = "PROG"
        };

        await page.OnGetAsync(CancellationToken.None);

        Assert.Equal(2, page.TotalResources);
        Assert.Contains(page.Resources, resource =>
            resource.Status == LearningResourceStatus.Draft);
        Assert.Contains(page.Resources, resource =>
            resource.Status == LearningResourceStatus.Published);

        page.SearchModule = "OTHER";
        await page.OnGetAsync(CancellationToken.None);

        Assert.Equal(0, page.TotalResources);
        Assert.Empty(page.Resources);
    }

    [Fact]
    public async Task IndexLimitsEachPageToThreeRowsOfThreeResources()
    {
        await using ApplicationDbContext context = CreateContext();
        List<LearningResource> resources = SeedResources(context);
        LearningResource seed = resources[0];
        for (int id = 3; id <= 10; id++)
        {
            context.LearningResources.Add(new LearningResource
            {
                LearningResourceId = id,
                TutorId = seed.TutorId,
                Tutor = seed.Tutor,
                ProgrammeModuleId = seed.ProgrammeModuleId,
                ProgrammeModule = seed.ProgrammeModule,
                Topic = $"Resource {id}",
                Content = $"Content {id}",
                Status = LearningResourceStatus.Published,
                DateCreated = DateTimeOffset.UtcNow.AddMinutes(-id),
                DatePublished = DateTimeOffset.UtcNow.AddMinutes(-id)
            });
        }
        await context.SaveChangesAsync();

        var firstPage = new AdminResourcesPage(context, CreateEnvironment());
        await firstPage.OnGetAsync(CancellationToken.None);

        Assert.Equal(9, AdminResourcesPage.PageSize);
        Assert.Equal(10, firstPage.TotalResources);
        Assert.Equal(2, firstPage.TotalPages);
        Assert.Equal(9, firstPage.Resources.Count);

        var secondPage = new AdminResourcesPage(context, CreateEnvironment())
        {
            ResourcePage = 2
        };
        await secondPage.OnGetAsync(CancellationToken.None);

        Assert.Single(secondPage.Resources);
    }

    [Fact]
    public async Task DeleteRemovesResourceAndItsDiscussion()
    {
        await using ApplicationDbContext context = CreateContext();
        LearningResource resource = SeedResources(context).First();
        var root = new ResourceComment
        {
            CommentId = 1,
            Resource = resource,
            ResourceId = resource.LearningResourceId,
            Author = resource.Tutor.BcUser,
            AuthorUserId = resource.Tutor.BcUserId,
            CommentText = "Question",
            DateCreated = DateTime.UtcNow
        };
        context.ResourceComments.AddRange(
            root,
            new ResourceComment
            {
                CommentId = 2,
                Resource = resource,
                ResourceId = resource.LearningResourceId,
                Author = resource.Tutor.BcUser,
                AuthorUserId = resource.Tutor.BcUserId,
                ParentComment = root,
                ParentCommentId = root.CommentId,
                CommentText = "Reply",
                DateCreated = DateTime.UtcNow
            });
        await context.SaveChangesAsync();
        var page = new AdminResourcesPage(context, CreateEnvironment());

        IActionResult result = await page.OnPostDeleteAsync(
            resource.LearningResourceId,
            CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.False(await context.LearningResources.AnyAsync(item =>
            item.LearningResourceId == resource.LearningResourceId));
        Assert.False(await context.ResourceComments.AnyAsync(comment =>
            comment.ResourceId == resource.LearningResourceId));
        UserNotification notification = await context.UserNotifications
            .SingleAsync();
        Assert.Equal(resource.Tutor.BcUserId, notification.RecipientBcUserId);
        Assert.Equal("Learning resource removed", notification.Title);
        Assert.Contains(resource.Topic, notification.Message);
        Assert.Equal("/Tutors/ManageResources", notification.LinkUrl);
    }

    [Fact]
    public async Task AdministratorCanViewDraftAndCommentWhenSubscriberCommentsAreClosed()
    {
        await using ApplicationDbContext context = CreateContext();
        List<LearningResource> resources = SeedResources(context);
        BcUser admin = new()
        {
            BcUserId = 50,
            PersonnelNumber = "ADMIN-1",
            DisplayName = "Admin User",
            Role = BcUserRole.Admin
        };
        context.BcUsers.Add(admin);
        resources[1].AllowSubscriberComments = false;
        await context.SaveChangesAsync();
        var currentUser = new CurrentUser(
            admin.BcUserId,
            admin.PersonnelNumber,
            admin.DisplayName,
            null,
            admin.Role);
        var details = new ResourceDetailsPage(
            context,
            new TestCurrentUserService(currentUser));

        IActionResult draftResult = await details.OnGetAsync(
            resources[0].LearningResourceId,
            CancellationToken.None);
        IActionResult commentResult = await details.OnPostAddCommentAsync(
            resources[1].LearningResourceId,
            "Administrator guidance",
            null,
            CancellationToken.None);

        Assert.IsType<PageResult>(draftResult);
        Assert.True(details.IsAdministrator);
        Assert.IsType<RedirectToPageResult>(commentResult);
        Assert.True(await context.ResourceComments.AnyAsync(comment =>
            comment.ResourceId == resources[1].LearningResourceId &&
            comment.AuthorUserId == admin.BcUserId &&
            comment.CommentText == "Administrator guidance"));
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static List<LearningResource> SeedResources(
        ApplicationDbContext context)
    {
        var programme = new ProgrammeOfStudy
        {
            Id = 1,
            Name = "Bachelor of Computing"
        };
        var module = new ProgrammeModule
        {
            ProgrammeModuleId = 1,
            ProgrammeId = programme.Id,
            Programme = programme,
            ModuleCode = "PROG101",
            ModuleName = "Programming",
            YearOfStudy = 1
        };
        var tutorUser = new BcUser
        {
            BcUserId = 1,
            PersonnelNumber = "TUTOR-1",
            DisplayName = "Resource Tutor",
            Role = BcUserRole.Tutor
        };
        var tutor = new Tutor
        {
            TutorId = 1,
            BcUserId = tutorUser.BcUserId,
            BcUser = tutorUser,
            ProgrammeId = programme.Id,
            Programme = programme,
            OverallAverage = 80,
            YearOfStudy = 2,
            ReasonForTutoring = "Reason",
            TeachingStyle = "Style",
            PreviousTutoringExperience = "Experience",
            PreferredTutoringMode = PreferredTutoringMode.Both,
            CampusOfStudy = "Pretoria",
            DemonstrationVideoUrl = "https://example.test/video",
            Status = TutorStatus.Approved,
            IsActive = true
        };
        var resources = new List<LearningResource>
        {
            new()
            {
                LearningResourceId = 1,
                TutorId = tutor.TutorId,
                Tutor = tutor,
                ProgrammeModuleId = module.ProgrammeModuleId,
                ProgrammeModule = module,
                Topic = "Draft notes",
                Content = "Draft content",
                Status = LearningResourceStatus.Draft,
                DateCreated = DateTimeOffset.UtcNow.AddDays(-2)
            },
            new()
            {
                LearningResourceId = 2,
                TutorId = tutor.TutorId,
                Tutor = tutor,
                ProgrammeModuleId = module.ProgrammeModuleId,
                ProgrammeModule = module,
                Topic = "Published notes",
                Content = "Published content",
                Status = LearningResourceStatus.Published,
                DateCreated = DateTimeOffset.UtcNow.AddDays(-1),
                DatePublished = DateTimeOffset.UtcNow
            }
        };
        context.LearningResources.AddRange(resources);
        return resources;
    }

    private static IWebHostEnvironment CreateEnvironment()
    {
        string root = Path.Combine(Path.GetTempPath(), "campuslearn-admin-resources");
        Directory.CreateDirectory(root);
        return new TestWebHostEnvironment(root);
    }

    private sealed class TestCurrentUserService(CurrentUser user)
        : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public CurrentUser GetRequiredUser() => user;
    }

    private sealed class TestWebHostEnvironment(string webRootPath)
        : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "BC_CampusLearn.Tests";
        public IFileProvider WebRootFileProvider { get; set; } =
            new NullFileProvider();
        public string WebRootPath { get; set; } = webRootPath;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = webRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }
}
