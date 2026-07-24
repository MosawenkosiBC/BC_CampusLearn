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
    public int? ProgrammeModuleId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? ProgrammeId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SearchName { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SearchModule { get; set; }

    [BindProperty(SupportsGet = true)]
    public List<int> Years { get; set; } = new();

    public IReadOnlyList<TutorCardViewModel> Tutors
    { get; private set; }
        = new List<TutorCardViewModel>();

    public List<SelectListItem> ModuleOptions
    { get; private set; }
        = new List<SelectListItem>();

    public List<SelectListItem> ProgrammeOptions
    { get; private set; } = new();

    public async Task OnGetAsync(
        CancellationToken cancellationToken)
    {
        var modules =
            await _tutorService.GetModulesAsync(
                cancellationToken);
        var programmes =
            await _tutorService.GetProgrammesAsync(
                cancellationToken);

        ModuleOptions = modules
            .Select(module =>
                new SelectListItem
                {
                    Value =
                        module.ProgrammeModuleId.ToString(),

                    Text =
                        $"{module.ModuleCode} - {module.ModuleName}"
                })
            .ToList();

        ProgrammeOptions = programmes
            .Select(programme => new SelectListItem
            {
                Value = programme.Id.ToString(),
                Text = programme.Name
            })
            .ToList();

        IReadOnlyList<TutorCardViewModel> tutors =
            await _tutorService.GetTutorsAsync(
                ProgrammeModuleId,
                cancellationToken);

        IEnumerable<TutorCardViewModel> filtered = tutors;

        if (!string.IsNullOrWhiteSpace(SearchName))
        {
            filtered = filtered.Where(tutor =>
                tutor.DisplayName.Contains(
                    SearchName.Trim(),
                    StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SearchModule))
        {
            string moduleSearch = SearchModule.Trim();
            filtered = filtered.Where(tutor =>
                tutor.Modules.Any(module =>
                    module.Contains(moduleSearch, StringComparison.OrdinalIgnoreCase)) ||
                tutor.ModuleCodes.Any(code =>
                    code.Contains(moduleSearch, StringComparison.OrdinalIgnoreCase)));
        }

        if (ProgrammeId.HasValue)
        {
            filtered = filtered.Where(tutor =>
                tutor.ProgrammeId == ProgrammeId.Value);
        }

        if (Years.Count > 0)
        {
            filtered = filtered.Where(tutor =>
                Years.Contains(tutor.YearOfStudy));
        }

        Tutors = filtered.ToList();
    }
}
