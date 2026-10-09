using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Pages.Administrator.Settings;
using BC_CampusLearn.Services.Announcements;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Xunit;
using AnnouncementDetailsModel = BC_CampusLearn.Pages.Announcements.DetailsModel;

namespace BC_CampusLearn.Tests;

public class AnnouncementTests
{
    private const string Content = """{"ops":[{"insert":"Tutor workshop","attributes":{"bold":true}},{"insert":"\nJoin us on Friday.\n"}]}""";

    [Theory]
    [InlineData(AnnouncementAudience.TutorsOnly, 2)]
    [InlineData(AnnouncementAudience.AllStudents, 5)]
    public async Task SendsOncePerEligibleRecipientAndKeepsRichContent(AnnouncementAudience audience, int expectedCount)
    {
        await using var context = CreateContext();
        await SeedAsync(context);
        var page = CreatePage(context);
        Assert.Equal(AnnouncementAudience.AllStudents, page.Input.Audience);
        page.Input = new() { Title = "Workshop", Content = Content, Audience = audience };
        Assert.IsType<RedirectToPageResult>(await page.OnPostAsync(default));
        var announcement = await context.Announcements.SingleAsync();
        Assert.Equal(Content, announcement.Content);
        var notifications = await context.UserNotifications.ToListAsync();
        Assert.Equal(expectedCount, notifications.Count);
        Assert.Equal(expectedCount, notifications.Select(item => item.RecipientBcUserId).Distinct().Count());
        Assert.DoesNotContain(notifications, item => item.RecipientBcUserId == 1);
        Assert.All(notifications, item =>
        {
            Assert.Equal(announcement.AnnouncementId, item.AnnouncementId);
            Assert.DoesNotContain("\"ops\"", item.Message);
            Assert.Contains(announcement.AnnouncementId.ToString(), item.LinkUrl);
            Assert.Null(item.ReadAt);
        });
        if (audience == AnnouncementAudience.TutorsOnly)
            Assert.Equal(new[] { 3, 6 }, notifications.Select(item => item.RecipientBcUserId).Order().ToArray());
        Assert.IsType<PageResult>(await page.OnGetAsync(default));
    }

    [Theory]
    [InlineData(BcUserRole.Student)]
    [InlineData(BcUserRole.Tutor)]
    [InlineData(BcUserRole.HeadOfTutors)]
    public async Task NonAdminCannotSendOrOpenComposer(BcUserRole role)
    {
        await using var context = CreateContext();
        var page = CreatePage(context, role);
        page.Input = new() { Title = "Workshop", Content = Content };
        Assert.IsType<ForbidResult>(await page.OnPostAsync(default));
        Assert.IsType<ForbidResult>(await page.OnGetAsync(default));
        Assert.Empty(await context.Announcements.ToListAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("""{"ops":[{"insert":"\n"}]}""")]
    [InlineData("""{"ops":[{"insert":{"image":"https://example.com/image.png"}}]}""")]
    [InlineData("""{"ops":[{"insert":"Click","attributes":{"link":"javascript:alert(1)"}}]}""")]
    [InlineData("""{"ops":[{"insert":"Click","attributes":{"link":"data:text/html,bad"}}]}""")]
    [InlineData("""{"ops":[{"insert":"Text","attributes":{"header":"bad"}}]}""")]
    [InlineData("""{"ops":[{"retain":5,"insert":"Text"}]}""")]
    public async Task RejectsEmptyOrUnsupportedContentWithoutSending(string content)
    {
        await using var context = CreateContext();
        await SeedAsync(context);
        var page = CreatePage(context);
        page.Input = new() { Title = "Workshop", Content = content };
        Assert.IsType<PageResult>(await page.OnPostAsync(default));
        Assert.False(page.ModelState.IsValid);
        Assert.Empty(await context.Announcements.ToListAsync());
        Assert.Empty(await context.UserNotifications.ToListAsync());
    }

    [Fact]
    public async Task RejectsUnknownAudienceAndEmptyRecipientList()
    {
        await using var context = CreateContext();
        var page = CreatePage(context);
        page.Input = new() { Title = "Workshop", Content = Content, Audience = (AnnouncementAudience)999 };
        Assert.IsType<PageResult>(await page.OnPostAsync(default));
        Assert.False(page.ModelState.IsValid);
        page = CreatePage(context);
        page.Input = new() { Title = "Workshop", Content = Content };
        Assert.IsType<PageResult>(await page.OnPostAsync(default));
        Assert.False(page.ModelState.IsValid);
        Assert.Empty(await context.Announcements.ToListAsync());
    }

    [Fact]
    public async Task OnlyOriginalRecipientsAndAdminsCanOpenAnnouncement()
    {
        await using var context = CreateContext();
        await SeedAsync(context);
        var page = CreatePage(context);
        page.Input = new() { Title = "Workshop", Content = Content, Audience = AnnouncementAudience.TutorsOnly };
        await page.OnPostAsync(default);
        var id = (await context.Announcements.SingleAsync()).AnnouncementId;
        var excluded = new AnnouncementDetailsModel(context, User(2, BcUserRole.Student), TimeProvider.System);
        Assert.IsType<NotFoundResult>(await excluded.OnGetAsync(id, default));
        var recipient = new AnnouncementDetailsModel(context, User(3, BcUserRole.Tutor), TimeProvider.System);
        Assert.IsType<PageResult>(await recipient.OnGetAsync(id, default));
        Assert.NotNull((await context.UserNotifications.SingleAsync(item => item.RecipientBcUserId == 3)).ReadAt);
        Assert.Null((await context.UserNotifications.SingleAsync(item => item.RecipientBcUserId == 6)).ReadAt);
        var admin = new AnnouncementDetailsModel(context, User(1, BcUserRole.Admin), TimeProvider.System);
        Assert.IsType<PageResult>(await admin.OnGetAsync(id, default));
        Assert.IsType<NotFoundResult>(await admin.OnGetAsync(Guid.NewGuid(), default));
    }

    [Fact]
    public void AcceptsPlainTextFallbackAndSafeFormattedLinks()
    {
        Assert.True(AnnouncementContent.TryGetText("Workshop details", out var text));
        Assert.Equal("Workshop details", text);
        Assert.True(AnnouncementContent.TryGetText(
            """{"ops":[{"insert":"Join","attributes":{"link":"https://example.com/workshop"}},{"insert":"\n","attributes":{"header":2}}]}""", out _));
        Assert.False(AnnouncementContent.TryGetText(new string('a', 100001), out _));
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static NotificationsModel CreatePage(ApplicationDbContext context, BcUserRole role = BcUserRole.Admin) =>
        new(context, User(1, role), TimeProvider.System);
    private static ICurrentUserService User(int id, BcUserRole role) => new TestUser(new(id, null, "Test user", null, role));
    private sealed class TestUser(CurrentUser user) : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public CurrentUser GetRequiredUser() => user;
    }

    private static async Task SeedAsync(ApplicationDbContext context)
    {
        context.BcUsers.AddRange(
            new BcUser { BcUserId = 1, DisplayName = "Admin", Role = BcUserRole.Admin },
            new BcUser { BcUserId = 2, DisplayName = "Student" },
            new BcUser { BcUserId = 3, DisplayName = "Active tutor" },
            new BcUser { BcUserId = 4, DisplayName = "Inactive tutor" },
            new BcUser { BcUserId = 5, DisplayName = "Pending tutor" },
            new BcUser { BcUserId = 6, DisplayName = "Senior tutor", Role = BcUserRole.SeniorTutor });
        foreach (var (id, status, active) in new[]
        {
            (3, TutorStatus.Approved, true), (4, TutorStatus.Approved, false),
            (5, TutorStatus.Pending, true), (6, TutorStatus.Approved, true)
        })
            context.Tutors.Add(new Tutor
            {
                TutorId = id, BcUserId = id, Status = status, IsActive = active,
                ApplicationStage = TutorApplicationStage.Placement,
                ReasonForTutoring = "", TeachingStyle = "", PreviousTutoringExperience = "",
                CampusOfStudy = "", DemonstrationVideoUrl = ""
            });
        await context.SaveChangesAsync();
    }
}
