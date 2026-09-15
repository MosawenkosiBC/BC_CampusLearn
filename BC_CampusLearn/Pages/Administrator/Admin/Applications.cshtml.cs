using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Administrator.Admin;

public class ApplicationsModel : PageModel
{
    private static readonly string[] ValidStages = { "applications", "shortlist", "interview", "placement" };
    private readonly ApplicationDbContext _context;

    public ApplicationsModel(ApplicationDbContext context) => _context = context;

    [BindProperty(SupportsGet = true)]
    public string Stage { get; set; } = "applications";

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    public bool OpenApplications { get; private set; }
    public int? ShortlistLimit { get; private set; }
    public int ShortlistRemaining => Math.Max((ShortlistLimit ?? 0) - ShortlistCount, 0);
    public int ApplicationCount { get; private set; }
    public int ShortlistCount { get; private set; }
    public int InterviewCount { get; private set; }
    public int PlacementCount { get; private set; }

    [TempData]
    public string? PageMessage { get; set; }

    [TempData]
    public string? PageError { get; set; }

    public IReadOnlyList<ApplicationCandidate> Candidates { get; private set; }
        = Array.Empty<ApplicationCandidate>();
    public IReadOnlyList<SelectListItem> StudentOptions { get; private set; }
        = Array.Empty<SelectListItem>();
    public IReadOnlyList<SelectListItem> ProgrammeOptions { get; private set; }
        = Array.Empty<SelectListItem>();

    [BindProperty]
    public ManualTutorInput ManualTutor { get; set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        NormalizeStage();
        await LoadSettingsAsync(cancellationToken);

        IQueryable<Tutor> applications = _context.Tutors.AsNoTracking();
        string? normalizedSearch = Search?.Trim();

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            applications = applications.Where(tutor =>
                tutor.BcUser.DisplayName.Contains(normalizedSearch) ||
                tutor.BcUser.PersonnelNumber.Contains(normalizedSearch) ||
                tutor.Programme.Name.Contains(normalizedSearch));
        }

        ApplicationCount = await applications.CountAsync(tutor =>
            tutor.Status == TutorStatus.Pending &&
            tutor.ApplicationStage == TutorApplicationStage.Submitted,
            cancellationToken);
        ShortlistCount = await applications.CountAsync(tutor =>
            tutor.Status == TutorStatus.Pending &&
            tutor.ApplicationStage == TutorApplicationStage.Shortlisted,
            cancellationToken);
        InterviewCount = await applications.CountAsync(tutor =>
            tutor.Status == TutorStatus.Pending &&
            tutor.ApplicationStage == TutorApplicationStage.Interview,
            cancellationToken);
        PlacementCount = await applications.CountAsync(tutor =>
            tutor.Status == TutorStatus.Approved ||
            tutor.ApplicationStage == TutorApplicationStage.Placement,
            cancellationToken);

        IQueryable<Tutor> candidates = Stage switch
        {
            "shortlist" => applications.Where(tutor =>
                tutor.Status == TutorStatus.Pending &&
                tutor.ApplicationStage == TutorApplicationStage.Shortlisted),
            "interview" => applications.Where(tutor =>
                tutor.Status == TutorStatus.Pending &&
                tutor.ApplicationStage == TutorApplicationStage.Interview),
            "placement" => applications.Where(tutor =>
                tutor.Status == TutorStatus.Approved ||
                tutor.ApplicationStage == TutorApplicationStage.Placement),
            _ => applications.Where(tutor =>
                tutor.Status == TutorStatus.Pending &&
                tutor.ApplicationStage == TutorApplicationStage.Submitted)
        };

        Candidates = await candidates
            .OrderByDescending(tutor => tutor.SubmittedAt)
            .Select(tutor => new ApplicationCandidate
            {
                TutorId = tutor.TutorId,
                DisplayName = string.IsNullOrWhiteSpace(tutor.BcUser.DisplayName)
                    ? tutor.BcUser.PersonnelNumber
                    : tutor.BcUser.DisplayName,
                PersonnelNumber = tutor.BcUser.PersonnelNumber,
                ProgrammeName = tutor.Programme.Name,
                YearOfStudy = tutor.YearOfStudy,
                OverallAverage = tutor.OverallAverage,
                ModuleCount = tutor.TutorCourseModules.Count,
                SubmittedAt = tutor.SubmittedAt,
                Status = tutor.Status,
                ApplicationStage = tutor.Status == TutorStatus.Approved
                    ? TutorApplicationStage.Placement
                    : tutor.ApplicationStage
            })
            .ToListAsync(cancellationToken);

        if (Stage == "placement")
        {
            await LoadManualTutorOptionsAsync(cancellationToken);
        }
    }

    public async Task<IActionResult> OnPostAddTutorAsync(
        CancellationToken cancellationToken)
    {
        BcUser? student = await _context.BcUsers
            .Include(user => user.Tutor)
            .SingleOrDefaultAsync(
                user => user.BcUserId == ManualTutor.BcUserId,
                cancellationToken);

        bool programmeExists = await _context.ProgrammesOfStudy
            .AnyAsync(
                programme => programme.Id == ManualTutor.ProgrammeId,
                cancellationToken);

        if (!ModelState.IsValid || student is null ||
            student.Role != BcUserRole.Student || student.Tutor is not null ||
            !programmeExists)
        {
            PageError = student?.Tutor is not null
                ? "This student already has a tutor profile."
                : "Enter valid details for the tutor you want to add.";
            return RedirectToPage(new { Stage = "placement" });
        }

        DateTime addedAt = DateTime.UtcNow;
        student.Role = BcUserRole.Tutor;
        student.Tutor = new Tutor
        {
            ProgrammeId = ManualTutor.ProgrammeId,
            OverallAverage = ManualTutor.OverallAverage,
            YearOfStudy = ManualTutor.YearOfStudy,
            PhoneNumber = ManualTutor.PhoneNumber?.Trim(),
            CampusOfStudy = ManualTutor.CampusOfStudy.Trim(),
            PreferredTutoringMode = ManualTutor.PreferredTutoringMode,
            ReasonForTutoring = "Added manually by an administrator.",
            TeachingStyle = "To be completed by the tutor.",
            PreviousTutoringExperience = "To be completed by the tutor.",
            DemonstrationVideoUrl = "Not provided",
            Status = TutorStatus.Approved,
            ApplicationStage = TutorApplicationStage.Placement,
            IsActive = true,
            SubmittedAt = addedAt,
            ReviewedAt = addedAt,
            CreatedAt = addedAt,
            UpdatedAt = addedAt
        };

        await _context.SaveChangesAsync(cancellationToken);
        PageMessage = $"{student.DisplayName} was added as a tutor.";
        return RedirectToPage(new { Stage = "placement" });
    }

    public async Task<IActionResult> OnPostSettingsAsync(
        bool isOpen,
        int? shortlistLimit,
        bool openWithoutTarget,
        bool notifyStudents,
        CancellationToken cancellationToken)
    {
        TutorApplicationSettings? settings = await _context
            .TutorApplicationSettings.SingleOrDefaultAsync(cancellationToken);

        if (settings is null)
        {
            settings = new TutorApplicationSettings();
            _context.TutorApplicationSettings.Add(settings);
        }

        if (isOpen)
        {
            int shortlisted = await _context.Tutors.CountAsync(tutor =>
                tutor.Status == TutorStatus.Pending &&
                tutor.ApplicationStage == TutorApplicationStage.Shortlisted,
                cancellationToken);

            if (openWithoutTarget)
            {
                settings.ShortlistLimit = null;
            }
            else
            {
                if (!shortlistLimit.HasValue || shortlistLimit.Value < 1)
                {
                    PageError = "Enter a shortlist target greater than zero.";
                    return RedirectToPage(new { Stage, Search });
                }

                if (shortlistLimit.Value < shortlisted)
                {
                    PageError = $"The target cannot be lower than the {shortlisted} candidates already shortlisted.";
                    return RedirectToPage(new { Stage, Search });
                }

                settings.ShortlistLimit = shortlistLimit.Value;
            }
        }

        settings.IsOpen = isOpen;
        settings.NotifyStudents = isOpen && notifyStudents;
        settings.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        PageMessage = isOpen
            ? settings.ShortlistLimit.HasValue
                ? $"Tutor applications are open with a shortlist target of {settings.ShortlistLimit}."
                : "Tutor applications are open without a shortlist target."
            : "Tutor applications are now closed.";
        return RedirectToPage(new { Stage, Search });
    }

    public async Task<IActionResult> OnPostShortlistAsync(
        int tutorId,
        bool finalizeThreshold,
        CancellationToken cancellationToken)
    {
        TutorApplicationSettings? settings = await _context
            .TutorApplicationSettings.SingleOrDefaultAsync(cancellationToken);

        if (settings is null)
        {
            PageError = "Configure the application cycle before reviewing candidates.";
            return RedirectToPage(new { Stage = "applications", Search });
        }

        Tutor? candidate = await _context.Tutors.SingleOrDefaultAsync(
            tutor => tutor.TutorId == tutorId,
            cancellationToken);

        if (candidate is null || candidate.Status != TutorStatus.Pending ||
            candidate.ApplicationStage != TutorApplicationStage.Submitted)
        {
            PageError = "This candidate is no longer awaiting review.";
            return RedirectToPage(new { Stage = "applications", Search });
        }

        int shortlisted = await _context.Tutors.CountAsync(tutor =>
            tutor.Status == TutorStatus.Pending &&
            tutor.ApplicationStage == TutorApplicationStage.Shortlisted,
            cancellationToken);
        int nextCount = shortlisted + 1;

        if (settings.ShortlistLimit.HasValue &&
            nextCount > settings.ShortlistLimit.Value)
        {
            PageError = "The shortlist target has already been reached. Increase the target before adding another candidate.";
            return RedirectToPage(new { Stage = "applications", Search });
        }

        bool reachesThreshold = settings.ShortlistLimit.HasValue &&
            nextCount == settings.ShortlistLimit.Value;
        if (reachesThreshold && !finalizeThreshold)
        {
            PageError = "Confirm that you want to fill the final shortlist place before continuing.";
            return RedirectToPage(new { Stage = "applications", Search });
        }

        DateTime reviewedAt = DateTime.UtcNow;
        candidate.ApplicationStage = TutorApplicationStage.Shortlisted;
        candidate.ReviewedAt = reviewedAt;
        candidate.UpdatedAt = reviewedAt;

        if (reachesThreshold)
        {
            List<Tutor> uncheckedApplications = await _context.Tutors
                .Where(tutor => tutor.TutorId != tutorId &&
                    tutor.Status == TutorStatus.Pending &&
                    tutor.ApplicationStage == TutorApplicationStage.Submitted)
                .ToListAsync(cancellationToken);

            foreach (Tutor uncheckedApplication in uncheckedApplications)
            {
                uncheckedApplication.Status = TutorStatus.Rejected;
                uncheckedApplication.ReviewedAt = reviewedAt;
                uncheckedApplication.UpdatedAt = reviewedAt;
            }

            PageMessage = $"Shortlist target reached. {uncheckedApplications.Count} unchecked applications were rejected.";
        }
        else
        {
            PageMessage = settings.ShortlistLimit.HasValue
                ? $"Candidate shortlisted. {settings.ShortlistLimit.Value - nextCount} places remain."
                : "Candidate shortlisted.";
        }

        await _context.SaveChangesAsync(cancellationToken);
        return RedirectToPage(new { Stage = "applications", Search });
    }

    private async Task LoadSettingsAsync(CancellationToken cancellationToken)
    {
        TutorApplicationSettings? settings = await _context
            .TutorApplicationSettings.AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        OpenApplications = settings?.IsOpen ?? false;
        ShortlistLimit = settings?.ShortlistLimit;
    }

    private void NormalizeStage()
    {
        Stage = Stage?.Trim().ToLowerInvariant() ?? "applications";
        if (!ValidStages.Contains(Stage, StringComparer.Ordinal))
        {
            Stage = "applications";
        }
    }

    private async Task LoadManualTutorOptionsAsync(
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
    }

    public sealed class ManualTutorInput
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
    }

    public sealed class ApplicationCandidate
    {
        public int TutorId { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public string PersonnelNumber { get; init; } = string.Empty;
        public string ProgrammeName { get; init; } = string.Empty;
        public int YearOfStudy { get; init; }
        public decimal OverallAverage { get; init; }
        public int ModuleCount { get; init; }
        public DateTime SubmittedAt { get; init; }
        public TutorStatus Status { get; init; }
        public TutorApplicationStage ApplicationStage { get; init; }

        public string Initials
        {
            get
            {
                string[] parts = DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                return parts.Length switch
                {
                    0 => "?",
                    1 => parts[0][..1].ToUpperInvariant(),
                    _ => $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant()
                };
            }
        }
    }
}
