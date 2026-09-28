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
