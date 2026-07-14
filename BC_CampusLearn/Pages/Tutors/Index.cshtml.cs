using BC_CampusLearn.Models.ViewModels;
using BC_CampusLearn.Services.Tutors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BC_CampusLearn.Pages.Tutors;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ITutorService _tutorService;

    public IndexModel(ITutorService tutorService)
    {
        _tutorService = tutorService;
    }

    [BindProperty(SupportsGet = true)]
    public int? CourseModuleId { get; set; }

    public IReadOnlyList<TutorCardViewModel> Tutors
    { get; private set; }
        = new List<TutorCardViewModel>();

    public List<SelectListItem> ModuleOptions
    { get; private set; }
        = new List<SelectListItem>();

    public async Task OnGetAsync(
        CancellationToken cancellationToken)
    {
        var modules =
            await _tutorService.GetModulesAsync(
                cancellationToken);

        ModuleOptions = modules
            .Select(module =>
                new SelectListItem
                {
                    Value =
                        module.CourseModuleId.ToString(),

                    Text =
                        $"{module.Code} - {module.Name}"
                })
            .ToList();

        Tutors =
            await _tutorService.GetTutorsAsync(
                CourseModuleId,
                cancellationToken);
    }
}