using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Pages.Notifications;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BC_CampusLearn.Tests;

public class NotificationsPageTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 18, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task NotificationsPageListsOnlyCurrentUsersItems()
    {
        await using ApplicationDbContext context = CreateContext();
        SeedNotifications(context);
        await context.SaveChangesAsync();
        var page = CreatePage(context);

        await page.OnGetAsync(CancellationToken.None);

        Assert.Equal(1, page.UnreadCount);
        Assert.Equal(2, page.Notifications.Count);
        Assert.DoesNotContain(page.Notifications, item => item.Title == "Other user");
        Assert.Contains(page.Notifications, item => item.ReadAt is null);
        Assert.Contains(page.Notifications, item => item.ReadAt is not null);
    }

    [Fact]
    public async Task MarkAllReadOnlyUpdatesCurrentUsersNotifications()
    {
        await using ApplicationDbContext context = CreateContext();
        SeedNotifications(context);
        await context.SaveChangesAsync();
        var page = CreatePage(context);

        await page.OnPostMarkAllReadAsync(1, CancellationToken.None);

        Assert.All(
            context.UserNotifications.Where(item => item.RecipientBcUserId == 1),
            item => Assert.NotNull(item.ReadAt));
        Assert.Null(context.UserNotifications
            .Single(item => item.Title == "Other user")
            .ReadAt);
    }

    [Fact]
    public async Task NotificationsPagePaginatesAfterEightItems()
    {
        await using ApplicationDbContext context = CreateContext();
        for (int index = 1; index <= 10; index++)
        {
            context.UserNotifications.Add(new UserNotification
            {
                RecipientBcUserId = 1,
                Title = $"Notification {index}",
                Message = $"Message {index}",
                LinkUrl = "/Student/Dashboard",
                CreatedAt = Now.AddMinutes(-index)
            });
        }
        await context.SaveChangesAsync();
        var page = CreatePage(context);

        await page.OnGetAsync(CancellationToken.None);

        Assert.Equal(2, page.TotalPages);
        Assert.Equal(8, page.Notifications.Count);

        page.CurrentPage = 2;
        await page.OnGetAsync(CancellationToken.None);

        Assert.Equal(2, page.Notifications.Count);
    }

    [Fact]
    public async Task OpeningNotificationModalMarksCurrentUsersItemAsRead()
    {
        await using ApplicationDbContext context = CreateContext();
        SeedNotifications(context);
        await context.SaveChangesAsync();
        var page = CreatePage(context);
        UserNotification ownUnread = await context.UserNotifications
            .SingleAsync(item => item.Title == "Own unread");
        page.NotificationId = ownUnread.UserNotificationId;
        await page.OnGetAsync(CancellationToken.None);

        Assert.Equal(Now, ownUnread.ReadAt);
        Assert.NotNull(page.SelectedNotification);
        Assert.Equal("Own unread", page.SelectedNotification.Title);
    }

    [Fact]
    public async Task SessionNotificationUsesViewSessionAction()
    {
        await using ApplicationDbContext context = CreateContext();
        context.UserNotifications.Add(new UserNotification
        {
            RecipientBcUserId = 1,
            Title = "Session starts in one hour",
            Message = "Detailed session reminder",
            LinkUrl = "/Bookings/SessionDetails/42",
            CreatedAt = Now
        });
        await context.SaveChangesAsync();
        var page = CreatePage(context);
        page.NotificationId = context.UserNotifications
            .Single()
            .UserNotificationId;

        await page.OnGetAsync(CancellationToken.None);

        Assert.NotNull(page.SelectedNotification);
        Assert.Equal("View session", page.SelectedNotification.ActionLabel);
    }

    [Fact]
    public void CompletedSessionNotificationUsesCompleteReviewAction()
    {
        var notification = new IndexModel.NotificationItem(
            42,
            "Session completed",
            "Please complete your review.",
            "/Bookings/SessionDetails/42",
            Now,
            null);

        Assert.Equal("Complete review", notification.ActionLabel);
    }

    [Fact]
    public void TutorApplicationNotificationUsesViewApplicationAction()
    {
        var notification = new IndexModel.NotificationItem(
            43,
            "Tutor application submitted",
            "Your application was submitted successfully.",
            "/Tutors/TutorApplication",
            Now,
            null);

        Assert.Equal("View application", notification.ActionLabel);
    }

    [Fact]
    public void OpenTutorApplicationsNotificationUsesApplyNowAction()
    {
        var notification = new IndexModel.NotificationItem(
            44,
            "🥳 Tutor applications are open",
            "Apply to become a peer tutor.\n• No failed subjects\n" +
                "Applications close: 31 October 2026",
            "/Tutors/TutorApplication",
            Now,
            null);

        Assert.Equal("Apply now", notification.ActionLabel);
        Assert.Equal(
            "Apply to become a peer tutor.\n• No failed subjects",
            notification.SummaryMessage);
        Assert.Equal(
            "Apply to become a peer tutor.",
            notification.PreviewMessage);
        Assert.Equal("31 October 2026", notification.HighlightedClosingDate);
    }

    [Theory]
    [InlineData("/Tutors/Profile")]
    [InlineData("/Tutors/PublicProfile")]
    public void TutorProfileNotificationUsesViewProfileAction(string linkUrl)
    {
        var notification = new IndexModel.NotificationItem(
            44,
            "Public profile updated",
            "Your tutor profile was updated.",
            linkUrl,
            Now,
            null);

        Assert.Equal("View profile", notification.ActionLabel);
    }

    [Fact]
    public async Task OpeningAnotherUsersNotificationDoesNotExposeOrUpdateIt()
    {
        await using ApplicationDbContext context = CreateContext();
        SeedNotifications(context);
        await context.SaveChangesAsync();
        var page = CreatePage(context);
        UserNotification otherUnread = await context.UserNotifications
            .SingleAsync(item => item.Title == "Other user");

        page.NotificationId = otherUnread.UserNotificationId;
        await page.OnGetAsync(CancellationToken.None);

        Assert.Null(page.SelectedNotification);
        Assert.Null(otherUnread.ReadAt);
        Assert.DoesNotContain(page.Notifications, item => item.Title == "Other user");
    }

    private static IndexModel CreatePage(ApplicationDbContext context) =>
        new(
            context,
            new TestCurrentUserService(new CurrentUser(
                1,
                "ST1001",
                "Student One",
                "student.one@example.com")),
            new FixedTimeProvider());

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static void SeedNotifications(ApplicationDbContext context)
    {
        context.UserNotifications.AddRange(
            new UserNotification
            {
                RecipientBcUserId = 1,
                Title = "Own unread",
                Message = "Unread notification",
                LinkUrl = "/Student/Dashboard",
                CreatedAt = Now.AddMinutes(-5)
            },
            new UserNotification
            {
                RecipientBcUserId = 1,
                Title = "Own read",
                Message = "Read notification",
                LinkUrl = "/Student/Dashboard",
                CreatedAt = Now.AddHours(-1),
                ReadAt = Now.AddMinutes(-30)
            },
            new UserNotification
            {
                RecipientBcUserId = 2,
                Title = "Other user",
                Message = "Must remain private",
                LinkUrl = "/Tutors/TutorDashboard",
                CreatedAt = Now.AddMinutes(-2)
            });
    }

    private sealed class TestCurrentUserService(CurrentUser currentUser)
        : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public CurrentUser GetRequiredUser() => currentUser;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
