using BC_CampusLearn.Data;
using BC_CampusLearn.Hubs;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Notifications;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BC_CampusLearn.Tests;

public class TutorHeadSessionReviewNotifierTests
{
    [Fact]
    public void NotificationMigrationIsDiscoverable()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=unused")
            .Options;
        using var context = new ApplicationDbContext(options);

        Assert.Contains(
            "20261001120000_AddTutorHeadSessionReviewNotifications",
            context.Database.GetMigrations());
    }

    [Fact]
    public async Task BothReviewsMarkSessionAvailableAndNotifyTutorHeads()
    {
        var proxy = new RecordingClientProxy();
        var clients = new RecordingHubClients(proxy);
        await using ApplicationDbContext context = CreateContext();
        context.Bookings.Add(new Booking
        {
            BookingId = 42,
            Status = BookingStatus.Completed,
            StudentEvaluation = new StudentEvaluation(),
            TutorEvaluation = new TutorStudentEvaluation()
        });
        await context.SaveChangesAsync();
        DateTimeOffset now = new(
            2026, 10, 1, 10, 0, 0, TimeSpan.Zero);
        var notifier = new TutorHeadSessionReviewNotifier(
            context,
            new TestHubContext(clients),
            new FixedTimeProvider(now),
            NullLogger<TutorHeadSessionReviewNotifier>.Instance);

        await notifier.NotifyIfAvailableAsync(42, CancellationToken.None);

        Assert.Equal(
            now,
            context.Bookings.Single().TutorHeadReviewAvailableAt);
        Assert.Equal(SessionHub.TutorHeadsGroupName, clients.GroupName);
        Assert.Equal("SessionReviewAvailable", proxy.MethodName);
    }

    [Fact]
    public async Task MissingReviewDoesNotNotifyTutorHeads()
    {
        var proxy = new RecordingClientProxy();
        var clients = new RecordingHubClients(proxy);
        await using ApplicationDbContext context = CreateContext();
        context.Bookings.Add(new Booking
        {
            BookingId = 42,
            Status = BookingStatus.Completed,
            StudentEvaluation = new StudentEvaluation()
        });
        await context.SaveChangesAsync();
        var notifier = new TutorHeadSessionReviewNotifier(
            context,
            new TestHubContext(clients),
            new FixedTimeProvider(DateTimeOffset.UtcNow),
            NullLogger<TutorHeadSessionReviewNotifier>.Instance);

        await notifier.NotifyIfAvailableAsync(42, CancellationToken.None);

        Assert.Null(context.Bookings.Single().TutorHeadReviewAvailableAt);
        Assert.Null(proxy.MethodName);
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
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

        public Task SendCoreAsync(
            string method,
            object?[] args,
            CancellationToken cancellationToken = default)
        {
            MethodName = method;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingHubClients(IClientProxy proxy)
        : IHubClients
    {
        public string? GroupName { get; private set; }
        public IClientProxy All => proxy;
        public IClientProxy AllExcept(IReadOnlyList<string> ids) => proxy;
        public IClientProxy Client(string id) => proxy;
        public IClientProxy Clients(IReadOnlyList<string> ids) => proxy;

        public IClientProxy Group(string groupName)
        {
            GroupName = groupName;
            return proxy;
        }

        public IClientProxy GroupExcept(
            string groupName,
            IReadOnlyList<string> ids) => proxy;
        public IClientProxy Groups(IReadOnlyList<string> names) => proxy;
        public IClientProxy User(string id) => proxy;
        public IClientProxy Users(IReadOnlyList<string> ids) => proxy;
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
