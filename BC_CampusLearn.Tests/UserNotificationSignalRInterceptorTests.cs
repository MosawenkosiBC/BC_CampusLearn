using BC_CampusLearn.Data;
using BC_CampusLearn.Hubs;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Notifications;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BC_CampusLearn.Tests;

public class UserNotificationSignalRInterceptorTests
{
    [Fact]
    public async Task SavingNotificationPushesItToRecipientInRealTime()
    {
        var proxy = new RecordingClientProxy();
        var clients = new RecordingHubClients(proxy);
        var interceptor = new UserNotificationSignalRInterceptor(
            new TestHubContext(clients),
            NullLogger<UserNotificationSignalRInterceptor>.Instance);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;
        await using var context = new ApplicationDbContext(options);
        context.UserNotifications.Add(new UserNotification
        {
            RecipientBcUserId = 21,
            Title = "Booking confirmed",
            Message = "Detailed session information",
            LinkUrl = "/Bookings/SessionDetails/42",
            CreatedAt = DateTimeOffset.UtcNow
        });

        await context.SaveChangesAsync();

        Assert.Equal("user-21", clients.RequestedGroupName);
        Assert.Equal("ReceiveUserNotification", proxy.MethodName);
        Assert.NotNull(proxy.Arguments);
        Assert.Single(proxy.Arguments!);
    }

    [Fact]
    public async Task SendingAnnouncementPushesToEachRecipientGroupAfterPersistence()
    {
        var proxy = new RecordingClientProxy();
        var clients = new RecordingHubClients(proxy);
        var interceptor = new UserNotificationSignalRInterceptor(
            new TestHubContext(clients), NullLogger<UserNotificationSignalRInterceptor>.Instance);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).AddInterceptors(interceptor).Options;
        await using var context = new ApplicationDbContext(options);
        context.BcUsers.AddRange(
            new BcUser { BcUserId = 1, DisplayName = "Admin", Role = BcUserRole.Admin },
            new BcUser { BcUserId = 2, DisplayName = "Student" },
            new BcUser { BcUserId = 3, DisplayName = "Tutor", Role = BcUserRole.SeniorTutor });
        await context.SaveChangesAsync();
        var page = new BC_CampusLearn.Pages.Administrator.Settings.NotificationsModel(
            context, new AnnouncementUser(), TimeProvider.System);
        page.Input = new()
        {
            Title = "Workshop", Content = "Join the workshop.",
            Audience = AnnouncementAudience.AllStudents
        };
        Assert.IsType<Microsoft.AspNetCore.Mvc.RedirectToPageResult>(await page.OnPostAsync(default));
        Assert.Single(await context.Announcements.ToListAsync());
        Assert.Equal(2, await context.UserNotifications.CountAsync());
        Assert.Equal(new[] { "user-2", "user-3" }, clients.RequestedGroups.Order().ToArray());
        Assert.Equal("ReceiveUserNotification", proxy.MethodName);
    }

    private sealed class AnnouncementUser : BC_CampusLearn.Authentication.ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public BC_CampusLearn.Authentication.CurrentUser GetRequiredUser() =>
            new(1, null, "Admin", null, BcUserRole.Admin);
    }
    private sealed class TestHubContext(IHubClients clients)
        : IHubContext<SessionHub>
    {
        public IHubClients Clients { get; } = clients;
        public IGroupManager Groups { get; } = new TestGroupManager();
    }

    private sealed class RecordingClientProxy : IClientProxy
    {
        public string? MethodName { get; private set; }
        public object?[]? Arguments { get; private set; }

        public Task SendCoreAsync(
            string method,
            object?[] args,
            CancellationToken cancellationToken = default)
        {
            MethodName = method;
            Arguments = args;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingHubClients(IClientProxy proxy)
        : IHubClients
    {
        public List<string> RequestedGroups { get; } = new();
        public string? RequestedGroupName { get; private set; }
        public IClientProxy All => proxy;
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => proxy;
        public IClientProxy Client(string connectionId) => proxy;
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => proxy;

        public IClientProxy Group(string groupName)
        {
            RequestedGroups.Add(groupName);
            RequestedGroupName = groupName;
            return proxy;
        }

        public IClientProxy GroupExcept(
            string groupName,
            IReadOnlyList<string> excludedConnectionIds) => proxy;

        public IClientProxy Groups(IReadOnlyList<string> groupNames) => proxy;
        public IClientProxy User(string userId) => proxy;
        public IClientProxy Users(IReadOnlyList<string> userIds) => proxy;
    }

    private sealed class TestGroupManager : IGroupManager
    {
        public Task AddToGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveFromGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
