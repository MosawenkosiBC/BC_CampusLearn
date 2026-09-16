using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Tutors;
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
    private readonly ICurrentUserService? _currentUserService;

    public ApplicationsModel(
        ApplicationDbContext context,
        ICurrentUserService? currentUserService = null)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    [BindProperty(SupportsGet = true)]
    public string Stage { get; set; } = "applications";

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    public bool OpenApplications { get; private set; }
    public int? ShortlistLimit { get; private set; }
    public DateTime? ApplicationOpenDate { get; private set; }
    public DateTime? ApplicationCloseDate { get; private set; }
    public int ApplicationCount { get; private set; }
    public int ShortlistCount { get; private set; }
    public int InterviewCount { get; private set; }
    public int PlacementCount { get; private set; }

    [TempData]
    public string? PageMessage { get; set; }

    [TempData]
    public bool ShowReviewResultModal { get; set; }

    [TempData]
    public string? PageError { get; set; }

    [TempData]
    public bool ShowShortlistThresholdModal { get; set; }

    [TempData]
    public bool ShowApplicationSettingsModal { get; set; }

    [TempData]
    public int? OpenShortlistCandidateId { get; set; }

    public IReadOnlyList<ApplicationCandidate> Candidates { get; private set; }
        = Array.Empty<ApplicationCandidate>();
    public IReadOnlyList<SelectListItem> StudentOptions { get; private set; }
        = Array.Empty<SelectListItem>();
    public IReadOnlyList<SelectListItem> ProgrammeOptions { get; private set; }
        = Array.Empty<SelectListItem>();

    [BindProperty]
    public ManualTutorInput ManualTutor { get; set; } = new();

    [BindProperty]
    public InterviewPreparationInput InterviewPreparation { get; set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        NormalizeStage();
        await TutorApplicationReview
            .RemoveRejectedApplicationsWhenCycleClosedAsync(
                _context,
                cancellationToken);
        await LoadSettingsAsync(cancellationToken);

        IQueryable<Tutor> applications = _context.Tutors
            .AsNoTracking()
            .Where(tutor =>
                tutor.Status != TutorStatus.Rejected &&
                tutor.ApplicationStage != TutorApplicationStage.Rejected);
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
                    : tutor.ApplicationStage,
                Email = tutor.BcUser.Email,
                PhoneNumber = tutor.PhoneNumber,
                CampusOfStudy = tutor.CampusOfStudy,
                PreferredTutoringMode = tutor.PreferredTutoringMode,
                ReasonForTutoring = tutor.ReasonForTutoring,
                TeachingStyle = tutor.TeachingStyle,
                PreviousTutoringExperience = tutor.PreviousTutoringExperience,
                DemonstrationVideoUrl = tutor.DemonstrationVideoUrl,
                InterviewPreparationNotes = tutor.InterviewPreparationNotes,
                InterviewScheduledAt = tutor.InterviewScheduledAt,
                InterviewDurationMinutes = tutor.InterviewDurationMinutes,
                InterviewLocation = tutor.InterviewLocation,
                AssignedInterviewer = tutor.AssignedInterviewer,
                DecisionHistory = tutor.ApplicationReviewDecisions
                    .OrderByDescending(decision => decision.ReviewedAt)
                    .Select(decision => new CandidateDecision
                    {
                        PreviousStage = decision.PreviousStage,
                        NewStage = decision.NewStage,
                        Notes = decision.Reason,
                        AdminName = decision.AdminName,
                        ReviewedAt = decision.ReviewedAt
                    })
                    .ToList(),
                Modules = tutor.TutorCourseModules
                    .OrderBy(item => item.ProgrammeModule.ModuleCode)
                    .Select(item => new CandidateModule
                    {
                        Code = item.ProgrammeModule.ModuleCode,
                        Name = item.ProgrammeModule.ModuleName
                    })
                    .ToList(),
                Documents = tutor.TutorDocuments
                    .OrderBy(document => document.DocumentType)
                    .Select(document => new CandidateDocument
                    {
                        TutorDocumentId = document.TutorDocumentId,
                        DocumentType = document.DocumentType,
                        OriginalFileName = document.OriginalFileName
                    })
                    .ToList()
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
        DateTime? openDate,
        DateTime? closeDate,
        CancellationToken cancellationToken)
    {
        TutorApplicationSettings? settings = await _context
            .TutorApplicationSettings.SingleOrDefaultAsync(cancellationToken);

        if (settings is null)
        {
            settings = new TutorApplicationSettings();
            _context.TutorApplicationSettings.Add(settings);
        }

        if (isOpen && (!shortlistLimit.HasValue || shortlistLimit.Value < 1 ||
            !openDate.HasValue || !closeDate.HasValue ||
            openDate.Value.Date > closeDate.Value.Date ||
            closeDate.Value.Date < DateTime.UtcNow.Date))
        {
            PageError = "Enter a shortlist target and a valid opening and closing date.";
            ShowApplicationSettingsModal = true;
            return RedirectToPage(new { Stage, Search });
        }

        settings.IsOpen = isOpen;
        if (isOpen)
        {
            settings.ShortlistLimit = shortlistLimit;
            settings.OpenDate = openDate!.Value.Date;
            settings.CloseDate = closeDate!.Value.Date;
            settings.ContinueAfterShortlistLimit = false;
        }
        settings.NotifyStudents = isOpen;
        settings.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        if (!settings.IsAcceptingApplications(DateTime.UtcNow))
        {
            await TutorApplicationReview
                .RemoveRejectedApplicationsWhenCycleClosedAsync(
                    _context,
                    cancellationToken);
        }

        PageMessage = isOpen
            ? "Tutor applications are now open."
            : "Tutor applications are now closed.";
        return RedirectToPage(new { Stage, Search });
    }

    public async Task<IActionResult> OnPostContinueShortlistingAsync(
        CancellationToken cancellationToken)
    {
        TutorApplicationSettings? settings = await _context
            .TutorApplicationSettings.SingleOrDefaultAsync(cancellationToken);
        if (settings is null || !settings.ShortlistLimit.HasValue)
        {
            PageError = "The application cycle settings could not be found.";
            return RedirectToPage(new { Stage = "shortlist", Search });
        }

        settings.ContinueAfterShortlistLimit = true;
        settings.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        PageMessage = "You can continue adding candidates beyond the shortlist target.";
        return RedirectToPage(new { Stage = "shortlist", Search });
    }

    public async Task<IActionResult> OnPostShortlistAsync(
        int tutorId,
        string? reviewReason,
        CancellationToken cancellationToken)
    {
        ShortlistResult result = await TutorApplicationReview.ShortlistAsync(
            _context,
            tutorId,
            reviewReason,
            cancellationToken,
            _currentUserService?.GetRequiredUser().BcUserId);
        if (!result.Succeeded)
        {
            PageError = result.Message;
            ShowShortlistThresholdModal = result.RequiresContinuation;
            return RedirectToPage(new
            {
                Stage = result.RequiresContinuation
                    ? "shortlist"
                    : "applications",
                Search
            });
        }

        PageMessage = result.Message;
        ShowShortlistThresholdModal = result.ShortlistLimitReached;
        return RedirectToPage(new
        {
            Stage = result.ShortlistLimitReached
                ? "shortlist"
                : "applications",
            Search
        });
    }

    public async Task<IActionResult> OnPostMoveToInterviewAsync(
        int tutorId,
        CancellationToken cancellationToken)
    {
        ShortlistResult result = await TutorApplicationReview.MoveToInterviewAsync(
            _context,
            tutorId,
            InterviewPreparation.ToDetails(),
            cancellationToken,
            _currentUserService?.GetRequiredUser().BcUserId);

        if (result.Succeeded)
        {
            PageMessage = result.Message;
        }
        else
        {
            PageError = result.Message;
        }

        return RedirectToPage(new { Stage = "shortlist", Search });
    }

    public async Task<IActionResult> OnPostSaveInterviewPreparationAsync(
        int tutorId,
        CancellationToken cancellationToken)
    {
        ShortlistResult result = await TutorApplicationReview
            .SaveInterviewPreparationAsync(
                _context,
                tutorId,
                InterviewPreparation.ToDetails(),
                cancellationToken,
                _currentUserService?.GetRequiredUser().BcUserId);

        OpenShortlistCandidateId = tutorId;
        if (result.Succeeded)
        {
            PageMessage = result.Message;
        }
        else
        {
            PageError = result.Message;
        }

        return RedirectToPage(new { Stage = "shortlist", Search });
    }

    public async Task<IActionResult> OnPostRejectShortlistedAsync(
        int tutorId,
        CancellationToken cancellationToken)
    {
        ShortlistResult result = await TutorApplicationReview.RejectShortlistedAsync(
            _context,
            tutorId,
            InterviewPreparation.Notes,
            cancellationToken,
            _currentUserService?.GetRequiredUser().BcUserId);

        if (result.Succeeded)
        {
            PageMessage = result.Message;
        }
        else
        {
            PageError = result.Message;
        }

        return RedirectToPage(new { Stage = "shortlist", Search });
    }

    private async Task LoadSettingsAsync(CancellationToken cancellationToken)
    {
        TutorApplicationSettings? settings = await _context
            .TutorApplicationSettings.AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        OpenApplications = settings?.IsOpen ?? false;
        ShortlistLimit = settings?.ShortlistLimit;
        ApplicationOpenDate = settings?.OpenDate;
        ApplicationCloseDate = settings?.CloseDate;
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

    public static string GetDocumentTypeLabel(TutorDocumentType type) =>
        type switch
        {
            TutorDocumentType.AcademicTranscript => "Academic transcript",
            TutorDocumentType.ExternalCertificate => "Additional certificate",
            _ => type.ToString()
        };

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

    public sealed class InterviewPreparationInput
    {
        [StringLength(1000)]
        public string? Notes { get; set; }

        [DataType(DataType.Date)]
        public DateTime? ScheduledDate { get; set; }

        [DataType(DataType.Time)]
        public TimeSpan? ScheduledTime { get; set; }

        [Range(15, 240)]
        public int? DurationMinutes { get; set; }

        [StringLength(500)]
        public string? LocationOrMeetingLink { get; set; }

        [StringLength(200)]
        public string? AssignedInterviewer { get; set; }

        public InterviewPreparationDetails ToDetails() => new(
            Notes,
            ScheduledDate,
            ScheduledTime,
            DurationMinutes,
            LocationOrMeetingLink,
            AssignedInterviewer);
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
        public string? Email { get; init; }
        public string? PhoneNumber { get; init; }
        public string CampusOfStudy { get; init; } = string.Empty;
        public PreferredTutoringMode PreferredTutoringMode { get; init; }
        public string ReasonForTutoring { get; init; } = string.Empty;
        public string TeachingStyle { get; init; } = string.Empty;
        public string PreviousTutoringExperience { get; init; } = string.Empty;
        public string DemonstrationVideoUrl { get; init; } = string.Empty;
        public string? InterviewPreparationNotes { get; init; }
        public DateTime? InterviewScheduledAt { get; init; }
        public int? InterviewDurationMinutes { get; init; }
        public string? InterviewLocation { get; init; }
        public string? AssignedInterviewer { get; init; }
        public IReadOnlyList<CandidateDecision> DecisionHistory { get; init; }
            = Array.Empty<CandidateDecision>();
        public IReadOnlyList<CandidateModule> Modules { get; init; }
            = Array.Empty<CandidateModule>();
        public IReadOnlyList<CandidateDocument> Documents { get; init; }
            = Array.Empty<CandidateDocument>();

        public string PreferredTutoringModeLabel => PreferredTutoringMode switch
        {
            PreferredTutoringMode.FaceToFace => "Face-To-Face",
            PreferredTutoringMode.Online => "Online",
            PreferredTutoringMode.Both => "Face-To-Face and online",
            _ => PreferredTutoringMode.ToString()
        };

        public bool HasDemonstrationVideoLink => Uri.TryCreate(
                DemonstrationVideoUrl,
                UriKind.Absolute,
                out Uri? uri) &&
            (uri.Scheme == Uri.UriSchemeHttps ||
                uri.Scheme == Uri.UriSchemeHttp);

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

    public sealed class CandidateModule
    {
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
    }

    public sealed class CandidateDocument
    {
        public int TutorDocumentId { get; init; }
        public TutorDocumentType DocumentType { get; init; }
        public string OriginalFileName { get; init; } = string.Empty;
    }

    public sealed class CandidateDecision
    {
        public TutorApplicationStage PreviousStage { get; init; }
        public TutorApplicationStage NewStage { get; init; }
        public string Notes { get; init; } = string.Empty;
        public string AdminName { get; init; } = string.Empty;
        public DateTime ReviewedAt { get; init; }

        public string Label => PreviousStage == NewStage
            ? "Preparation notes saved"
            : NewStage switch
            {
                TutorApplicationStage.Shortlisted => "Application shortlisted",
                TutorApplicationStage.Interview => "Moved to interview",
                TutorApplicationStage.Placement => "Moved to placement",
                TutorApplicationStage.Rejected => "Application rejected",
                _ => $"Moved to {NewStage}"
            };
    }
}
