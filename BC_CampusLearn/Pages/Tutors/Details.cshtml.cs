using BC_CampusLearn.Models.ViewModels;
using BC_CampusLearn.Services.Tutors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BC_CampusLearn.Pages.Tutors;

[Authorize]
public class DetailsModel : PageModel
{
    private readonly ITutorService _tutorService;

    public DetailsModel(ITutorService tutorService)
    {
        _tutorService = tutorService;
    }

    public TutorDetailsViewModel Tutor { get; private set; }
        = null!;

    public async Task<IActionResult> OnGetAsync(
        int id,
        CancellationToken cancellationToken)
    {
        TutorDetailsViewModel? tutor =
            await _tutorService.GetTutorDetailsAsync(
                id,
                cancellationToken);

        if (tutor is null)
        {
            return NotFound();
        }

        Tutor = tutor;

        return Page();
    }
}