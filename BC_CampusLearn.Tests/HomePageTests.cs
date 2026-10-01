using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using BC_CampusLearn.Services.Tutors;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BC_CampusLearn.Tests;

public class HomePageTests
{
    [Fact]
    public async Task ShowsTutorApplicationCallToActionDuringOpenCycle()
    {
        await using ApplicationDbContext context = CreateContext();
        DateTime today = DateTime.UtcNow.Date;
        context.TutorApplicationSettings.Add(new TutorApplicationSettings
        {
            IsOpen = true,
            OpenDate = today.AddDays(-1),
            CloseDate = today.AddDays(1)
        });
        await context.SaveChangesAsync();

        var page = new Pages.IndexModel(context, new EmptyTutorService());

        await page.OnGetAsync(CancellationToken.None);

        Assert.True(page.TutorApplicationsOpen);
    }

    [Theory]
    [InlineData(false, -1, 1)]
    [InlineData(true, 1, 2)]
    [InlineData(true, -2, -1)]
    public async Task HidesTutorApplicationCallToActionOutsideOpenCycle(
        bool isOpen,
        int opensInDays,
        int closesInDays)
    {
        await using ApplicationDbContext context = CreateContext();
        DateTime today = DateTime.UtcNow.Date;
        context.TutorApplicationSettings.Add(new TutorApplicationSettings
        {
            IsOpen = isOpen,
            OpenDate = today.AddDays(opensInDays),
            CloseDate = today.AddDays(closesInDays)
        });
        await context.SaveChangesAsync();

        var page = new Pages.IndexModel(context, new EmptyTutorService());

        await page.OnGetAsync(CancellationToken.None);

        Assert.False(page.TutorApplicationsOpen);
    }

    [Fact]
    public async Task ShowsOnlyPublishedEventsThatHaveNotEnded()
    {
        await using ApplicationDbContext context = CreateContext();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        context.CampusEvents.AddRange(
            CreateEvent("Visible event", now.AddDays(-1), now.AddDays(1),
                now.AddDays(-2), true),
            CreateEvent("Draft event", now.AddDays(1), now.AddDays(2),
                now.AddDays(-1), false),
            CreateEvent("Scheduled announcement", now.AddDays(2), now.AddDays(3),
                now.AddHours(1), true),
            CreateEvent("Ended event", now.AddDays(-2), now.AddDays(-1),
                now.AddDays(-3), true));
        await context.SaveChangesAsync();

        var page = new Pages.IndexModel(context, new EmptyTutorService());
        await page.OnGetAsync(CancellationToken.None);

        var visible = Assert.Single(page.ActiveEvents);
        Assert.Equal("Visible event", visible.Title);
    }

    private static CampusEvent CreateEvent(
        string title,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        DateTimeOffset publishAt,
        bool isPublished) => new()
    {
        Title = title,
        Description = "Event description",
        Location = "Pretoria Campus",
        StartsAt = startsAt,
        EndsAt = endsAt,
        PublishAt = publishAt,
        IsPublished = isPublished,
        BannerImagePath = "/uploads/events/banner.jpg",
        CreatedAt = DateTimeOffset.UtcNow
    };

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private sealed class EmptyTutorService : ITutorService
    {
        public Task<IReadOnlyList<TutorCardViewModel>> GetTutorsAsync(
            int? programmeModuleId,
            string? preferredCampus,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TutorCardViewModel>>(
                Array.Empty<TutorCardViewModel>());

        public Task<TutorDetailsViewModel?> GetTutorDetailsAsync(
            int tutorId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<TutorDetailsViewModel?>(null);

        public Task<IReadOnlyList<ProgrammeModule>> GetModulesAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProgrammeModule>>([]);

        public Task<IReadOnlyList<ProgrammeOfStudy>> GetProgrammesAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProgrammeOfStudy>>([]);

        public Task<IReadOnlyList<string>> GetCampusesAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);
    }
}
