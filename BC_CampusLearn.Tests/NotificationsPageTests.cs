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
