using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using BC_CampusLearn.Services.Tutors;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly ITutorService _tutorService;

    public IndexModel(
        ApplicationDbContext context,
        ITutorService tutorService)
    {
        _context = context;
        _tutorService = tutorService;
    }

    public bool TutorApplicationsOpen { get; private set; }

    public IReadOnlyList<TutorCardViewModel> FeaturedTutors
    { get; private set; } = Array.Empty<TutorCardViewModel>();

    public IReadOnlyList<HomeEventViewModel> ActiveEvents
    { get; private set; } = Array.Empty<HomeEventViewModel>();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        TutorApplicationSettings? applicationSettings = await _context
            .TutorApplicationSettings
            .AsNoTracking()
            .Where(settings => settings.TutorApplicationSettingsId ==
                TutorApplicationSettings.SingletonId)
            .SingleOrDefaultAsync(cancellationToken);
        TutorApplicationsOpen = applicationSettings?.IsAcceptingApplications(
            DateTime.UtcNow) ?? false;

        FeaturedTutors = (await _tutorService.GetTutorsAsync(
                programmeModuleId: null,
                preferredCampus: null,
                cancellationToken))
            .Take(4)
            .ToList();

        DateTimeOffset now = DateTimeOffset.UtcNow;
        ActiveEvents = await _context.CampusEvents
            .AsNoTracking()
            .Where(item => item.IsPublished &&
                item.PublishAt <= now &&
                item.EndsAt >= now)
            .OrderBy(item => item.StartsAt)
            .Select(item => new HomeEventViewModel
            {
                CampusEventId = item.CampusEventId,
                Title = item.Title,
                Description = item.Description,
                Disclaimer = item.Disclaimer,
                Location = item.Location,
                StartsAt = item.StartsAt,
                EndsAt = item.EndsAt,
                BannerImagePath = item.BannerImagePath,
                Details = item.Details.OrderBy(detail => detail.Position)
                    .Select(detail => new HomeEventDetailViewModel
                    {
                        Title = detail.Title,
                        DataType = detail.DataType,
                        Value = detail.Value
                    }).ToList()
            })
            .ToListAsync(cancellationToken);
    }
}

public sealed class HomeEventViewModel
{
    public int CampusEventId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Disclaimer { get; set; }
    public string Location { get; set; } = string.Empty;
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public string BannerImagePath { get; set; } = string.Empty;
    public List<HomeEventDetailViewModel> Details { get; set; } = [];
}

public sealed class HomeEventDetailViewModel
{
    public string Title { get; set; } = string.Empty;
    public CampusEventDetailDataType DataType { get; set; }
    public string Value { get; set; } = string.Empty;
}
