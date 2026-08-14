using BC_CampusLearn.Models.ViewModels;
using BC_CampusLearn.Services.Tutors;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BC_CampusLearn.Pages;

public class IndexModel : PageModel
{
    private readonly ITutorService _tutorService;

    public IndexModel(ITutorService tutorService)
    {
        _tutorService = tutorService;
    }

    public IReadOnlyList<TutorCardViewModel> FeaturedTutors
    { get; private set; } = Array.Empty<TutorCardViewModel>();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        FeaturedTutors = (await _tutorService.GetTutorsAsync(
                programmeModuleId: null,
                cancellationToken))
            .Take(4)
            .ToList();
    }
}
