using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Administrator;

// Shared manual tutor workflow for the placement and tutor directory pages.
public abstract class ManualTutorPageModel(ApplicationDbContext context) : PageModel
{
    private readonly ApplicationDbContext _context = context;

    protected virtual IActionResult RedirectAfterAddTutor() => RedirectToPage();

    public static readonly IReadOnlyList<string> ManualTutorCampuses =
    [
        "Pretoria Campus",
        "Kempton Park Campus",
        "Stellenbosch Campus",
        "Online"
    ];
    [TempData]
    public string? PageMessage { get; set; }

    [TempData]
    public string? PageError { get; set; }

    public IReadOnlyList<SelectListItem> StudentOptions { get; private set; }
        = Array.Empty<SelectListItem>();
    public IReadOnlyList<SelectListItem> ProgrammeOptions { get; private set; }
        = Array.Empty<SelectListItem>();
    public IReadOnlyList<ManualModuleOption> ManualTutorModuleOptions
    { get; private set; } = Array.Empty<ManualModuleOption>();

    [BindProperty]
    public ManualTutorInput ManualTutor { get; set; } = new();

    public async Task<IActionResult> OnPostAddTutorAsync(
        CancellationToken cancellationToken)
    {
        // This page contains several independent forms. Validate only the
        // manual tutor input so application/interview fields cannot block it.
        ModelState.Clear();
        var validationResults = new List<ValidationResult>();
        bool manualInputIsValid = Validator.TryValidateObject(
            ManualTutor,
            new ValidationContext(ManualTutor),
            validationResults,
            validateAllProperties: true);

        BcUser? student = await _context.BcUsers
            .Include(user => user.Tutor)
            .SingleOrDefaultAsync(
                user => user.BcUserId == ManualTutor.BcUserId,
                cancellationToken);

        bool programmeExists = await _context.ProgrammesOfStudy
            .AnyAsync(
                programme => programme.Id == ManualTutor.ProgrammeId,
                cancellationToken);

        if (!manualInputIsValid)
        {
            PageError = "Enter valid details for the tutor you want to add.";
            return RedirectAfterAddTutor();
        }

        if (student is null)
        {
            PageError = "Select an eligible student.";
            return RedirectAfterAddTutor();
        }

        if (student.Tutor is not null)
        {
            PageError = "This student already has a tutor profile.";
            return RedirectAfterAddTutor();
        }

        if (student.Role != BcUserRole.Student)
        {
            PageError = "Only students can be added as tutors.";
            return RedirectAfterAddTutor();
        }

        if (!programmeExists)
        {
            PageError = "Select a valid programme.";
            return RedirectAfterAddTutor();
        }

        List<int> moduleIds = ManualTutor.ProgrammeModuleIds
            .Distinct()
            .ToList();
        if (moduleIds.Count == 0)
        {
            PageError = "Select at least one module for the tutor.";
            return RedirectAfterAddTutor();
        }

        int eligibleModuleCount = await _context.ProgrammeModules
            .AsNoTracking()
            .CountAsync(
                module => moduleIds.Contains(module.ProgrammeModuleId) &&
                    module.ProgrammeId == ManualTutor.ProgrammeId &&
                    module.YearOfStudy <= ManualTutor.YearOfStudy,
                cancellationToken);
        if (eligibleModuleCount != moduleIds.Count)
        {
            PageError = "Select modules from the chosen programme that are eligible for the tutor's year of study.";
            return RedirectAfterAddTutor();
        }

        DateTime addedAt = DateTime.UtcNow;
        student.Role = BcUserRole.Tutor;
        var tutor = new Tutor
        {
            ProgrammeId = ManualTutor.ProgrammeId,
            OverallAverage = ManualTutor.OverallAverage,
            YearOfStudy = ManualTutor.YearOfStudy,
            PhoneNumber = string.IsNullOrWhiteSpace(ManualTutor.PhoneNumber)
                ? null
                : ManualTutor.PhoneNumber.Trim(),
            CampusOfStudy = ManualTutor.CampusOfStudy.Trim(),
            PreferredTutoringMode = ManualTutor.PreferredTutoringMode,
            ReasonForTutoring = "",
            TeachingStyle = "",
            PreviousTutoringExperience = "",
            DemonstrationVideoUrl = "",
            Status = TutorStatus.Approved,
            ApplicationStage = TutorApplicationStage.Placement,
            IsActive = true,
            SubmittedAt = addedAt,
            ReviewedAt = addedAt,
            CreatedAt = addedAt,
            UpdatedAt = addedAt
        };

        foreach (int moduleId in moduleIds)
        {
            tutor.TutorCourseModules.Add(new TutorCourseModule
            {
                ProgrammeModuleId = moduleId
            });
        }
        student.Tutor = tutor;

        await _context.SaveChangesAsync(cancellationToken);
        PageMessage = $"{student.DisplayName} was added as a tutor.";
        return RedirectAfterAddTutor();
    }

    protected async Task LoadManualTutorOptionsAsync(
        CancellationToken cancellationToken)
    {
        StudentOptions = await _context.BcUsers
            .AsNoTracking()
            .Where(user => user.Role == BcUserRole.Student &&
                user.Tutor == null)
            .OrderBy(user => user.DisplayName)
            .Select(user => new SelectListItem
            {
                Value = user.BcUserId.ToString(),
                Text = user.DisplayName + " (" + user.PersonnelNumber + ")"
            })
            .ToListAsync(cancellationToken);

        ProgrammeOptions = await _context.ProgrammesOfStudy
            .AsNoTracking()
            .OrderBy(programme => programme.Name)
            .Select(programme => new SelectListItem
            {
                Value = programme.Id.ToString(),
                Text = programme.Name
            })
            .ToListAsync(cancellationToken);

        ManualTutorModuleOptions = await _context.ProgrammeModules
            .AsNoTracking()
            .OrderBy(module => module.Programme.Name)
            .ThenBy(module => module.YearOfStudy)
            .ThenBy(module => module.ModuleCode)
            .Select(module => new ManualModuleOption
            {
                ProgrammeModuleId = module.ProgrammeModuleId,
                ProgrammeId = module.ProgrammeId,
                YearOfStudy = module.YearOfStudy,
                Code = module.ModuleCode,
                Name = module.ModuleName
            })
            .ToListAsync(cancellationToken);
    }

    public sealed class ManualTutorInput : IValidatableObject
    {
        [Range(1, int.MaxValue)]
        public int BcUserId { get; set; }

        [Range(1, int.MaxValue)]
        public int ProgrammeId { get; set; }

        [Range(1, 4)]
        public int YearOfStudy { get; set; }

        [Range(typeof(decimal), "0", "100")]
        public decimal OverallAverage { get; set; }

        [Required, StringLength(100)]
        public string CampusOfStudy { get; set; } = string.Empty;

        [Phone, StringLength(32)]
        public string? PhoneNumber { get; set; }

        public PreferredTutoringMode PreferredTutoringMode { get; set; }
            = PreferredTutoringMode.Both;

        public List<int> ProgrammeModuleIds { get; set; } = [];

        public IEnumerable<ValidationResult> Validate(
            ValidationContext validationContext)
        {
            if (!ManualTutorCampuses.Contains(
                CampusOfStudy,
                StringComparer.Ordinal))
            {
                yield return new ValidationResult(
                    "Select a valid campus.",
                    [nameof(CampusOfStudy)]);
            }
        }
    }

    public sealed class ManualModuleOption
    {
        public int ProgrammeModuleId { get; init; }
        public int ProgrammeId { get; init; }
        public int YearOfStudy { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
    }

}
