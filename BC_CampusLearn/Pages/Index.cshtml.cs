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
    }
}
