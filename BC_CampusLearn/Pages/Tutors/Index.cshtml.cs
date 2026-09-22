using BC_CampusLearn.Authentication;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using BC_CampusLearn.Services.Students;
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
    private readonly ICurrentUserService _currentUserService;
    private readonly IStudentDetailsService _studentDetailsService;

    public IndexModel(
        ITutorService tutorService,
        ICurrentUserService currentUserService,
        IStudentDetailsService studentDetailsService)
    {
        _tutorService = tutorService;
        _currentUserService = currentUserService;
        _studentDetailsService = studentDetailsService;
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
    public PreferredTutoringMode? TutoringMode { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Campus { get; set; }

    public IReadOnlyList<TutorCardViewModel> Tutors
    { get; private set; }
        = new List<TutorCardViewModel>();

    public List<SelectListItem> ModuleOptions
    { get; private set; }
        = new List<SelectListItem>();

    public List<SelectListItem> ProgrammeOptions
    { get; private set; } = new();

    public List<SelectListItem> CampusOptions
    { get; private set; } = new();

    public async Task OnGetAsync(
        CancellationToken cancellationToken)
    {
        CurrentUser currentUser = _currentUserService.GetRequiredUser();
        bool hasStudentCampus = currentUser.Role is
            BcUserRole.Student or
            BcUserRole.Tutor or
            BcUserRole.HeadOfTutors;
        string? preferredCampus = null;

        if (hasStudentCampus)
        {
            StudentDetailsResult studentDetails =
                await _studentDetailsService.GetAsync(
                    currentUser.PersonnelNumber,
                    cancellationToken);

            if (studentDetails is
                { Status: StudentDetailsStatus.Success, Details: not null })
            {
                preferredCampus = studentDetails.Details.Campus;
            }
        }

        var modules =
            await _tutorService.GetModulesAsync(
                cancellationToken);
        var programmes =
            await _tutorService.GetProgrammesAsync(
                cancellationToken);
        IReadOnlyList<string> campuses =
            await _tutorService.GetCampusesAsync(
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
                preferredCampus,
                cancellationToken);

        CampusOptions = campuses
            .Select(campus => campus.Trim())
            .Where(campus => !string.IsNullOrWhiteSpace(campus))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(campus => campus)
            .Select(campus => new SelectListItem
            {
                Value = campus,
                Text = campus
            })
            .ToList();

        IEnumerable<TutorCardViewModel> filtered = tutors;

        if (!string.IsNullOrWhiteSpace(Campus))
        {
            string? selectedCampus = CampusOptions
                .Select(option => option.Value)
                .SingleOrDefault(value => string.Equals(
                    value,
                    Campus.Trim(),
                    StringComparison.OrdinalIgnoreCase));

            if (selectedCampus is null)
            {
                Campus = null;
            }
            else
            {
                Campus = selectedCampus;
                filtered = filtered.Where(tutor => string.Equals(
                    tutor.CampusOfStudy.Trim(),
                    selectedCampus,
                    StringComparison.OrdinalIgnoreCase));
            }
        }

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

        if (TutoringMode.HasValue)
        {
            filtered = filtered.Where(tutor =>
                tutor.PreferredTutoringMode == TutoringMode.Value);
        }

        Tutors = filtered.ToList();
    }
}
