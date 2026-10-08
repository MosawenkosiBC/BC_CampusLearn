using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using BC_CampusLearn.Services.Tutors;
using BC_CampusLearn.Services.Students;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Administrator.Admin;

public class ApplicationsModel : ManualTutorPageModel
{
    protected override IActionResult RedirectAfterAddTutor() =>
        RedirectToPage(new { Stage = "placement" });

    private static readonly string[] ValidStages = { "applications", "shortlist", "interview", "placement" };
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService? _currentUserService;
    private readonly ITutorApplicationEmailSender? _emailSender;

    public ApplicationsModel(
        ApplicationDbContext context,
        ICurrentUserService? currentUserService = null,
        ITutorApplicationEmailSender? emailSender = null,
        IStudentDetailsService? studentDetailsService = null) : base(context, studentDetailsService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _emailSender = emailSender;
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
    public bool ShowReviewResultModal { get; set; }

    [TempData]
    public bool ShowShortlistThresholdModal { get; set; }

    [TempData]
    public bool ShowApplicationSettingsModal { get; set; }

    [TempData]
    public int? OpenShortlistCandidateId { get; set; }

    [TempData]
    public int? OpenInterviewRoomCandidateId { get; set; }

    [TempData]
    public string? InterviewRoomMessage { get; set; }

    public IReadOnlyList<ApplicationCandidate> Candidates { get; private set; }
        = Array.Empty<ApplicationCandidate>();
    [BindProperty]
    public InterviewPreparationInput InterviewPreparation { get; set; } = new();

    [BindProperty]
    public ApplicationMessageInput Communication { get; set; } = new();

    [BindProperty]
    public ApplicationMessageInput RejectionMessage { get; set; } = new();

    [BindProperty]
    [StringLength(4000, ErrorMessage = "Interview notes cannot exceed 4,000 characters.")]
    public string? InterviewNotes { get; set; }

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
                (tutor.Status == TutorStatus.Pending ||
                    (tutor.Status == TutorStatus.Approved && tutor.IsActive)) &&
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
                ModuleCount = tutor.TutorCourseModules.Count(item => item.IsActive),
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
                InterviewNotes = tutor.InterviewNotes,
                InterviewScheduledAt = tutor.InterviewScheduledAt,
                InterviewDurationMinutes = tutor.InterviewDurationMinutes,
                InterviewLocation = tutor.InterviewLocation,
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
                    .Where(item => item.IsActive)
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

    public async Task<IActionResult> OnPostSettingsAsync(
        bool isOpen,
        int? shortlistLimit,
        DateTime? openDate,
        DateTime? closeDate,
        CancellationToken cancellationToken)
    {
        TutorApplicationSettings? settings = await _context
            .TutorApplicationSettings.SingleOrDefaultAsync(cancellationToken);
        bool applicationsWereOpen = settings?.IsOpen ?? false;

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

        if (isOpen && !applicationsWereOpen)
        {
            List<int> studentIds = await _context.BcUsers
                .AsNoTracking()
                .Where(user => user.Role == BcUserRole.Student)
                .Select(user => user.BcUserId)
                .ToListAsync(cancellationToken);
            DateTimeOffset createdAt = DateTimeOffset.UtcNow;

            _context.UserNotifications.AddRange(studentIds.Select(studentId =>
                new UserNotification
                {
                    RecipientBcUserId = studentId,
                    Title = "🥳 Tutor applications are open",
                    Message = "Interested in helping fellow students? Apply " +
                        "to become a peer tutor.\n\nWhat you'll need:\n" +
                        "• 65%+ overall average or 75%+ in a subject\n" +
                        "• No failed subjects\n" +
                        "• 8-15 hours per month to tutor\n" +
                        $"Applications close: {closeDate:dd MMMM yyyy}",
                    LinkUrl = "/Tutors/TutorApplication",
                    CreatedAt = createdAt
                }));
        }

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
        return RedirectToPage(new
        {
            Stage = isOpen ? Stage : "applications",
            Search
        });
    }

    public async Task<IActionResult> OnPostContinueShortlistingAsync(
        CancellationToken cancellationToken)
    {
        TutorApplicationSettings? settings = await _context
            .TutorApplicationSettings.SingleOrDefaultAsync(cancellationToken);
        if (settings is null || !settings.ShortlistLimit.HasValue)
        {
            PageError = "The application cycle settings could not be found.";
            return RedirectToPage(new { Stage = "applications", Search });
        }

        settings.ContinueAfterShortlistLimit = true;
        settings.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        PageMessage = "You can continue adding candidates beyond the shortlist target.";
        return RedirectToPage(new { Stage = "applications", Search });
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
            return RedirectToPage(new { Stage = "applications", Search });
        }

        PageMessage = result.Message;
        ShowShortlistThresholdModal = result.ShortlistLimitReached;
        return RedirectToPage(new { Stage = "applications", Search });
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

    public async Task<IActionResult> OnPostSendCommunicationAsync(
        int tutorId,
        CancellationToken cancellationToken)
    {
        string? validationError = ValidateComposedMessage(
            Communication,
            requireNotificationMessage: false);
        if (validationError is not null)
        {
            PageError = validationError;
            return RedirectToPage(new { Stage, Search });
        }

        var recipient = await GetCommunicationRecipientAsync(
            tutorId,
            cancellationToken);
        if (recipient is null || string.IsNullOrWhiteSpace(recipient.Email))
        {
            PageError = "The candidate does not have an email address.";
            return RedirectToPage(new { Stage, Search });
        }

        _context.UserNotifications.Add(new UserNotification
        {
            RecipientBcUserId = recipient.BcUserId,
            Title = "Tutor application communication sent",
            Message = $"The Mzala Connect Tutor Team sent you an email about your tutor application. Please check {recipient.Email} for the full message.",
            LinkUrl = "/Tutors/TutorApplication",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await _context.SaveChangesAsync(cancellationToken);

        if (_emailSender is not null)
        {
            await _emailSender.SendComposedAsync(
                recipient.Email,
                Communication.Subject.Trim(),
                Communication.EmailBody.Trim(),
                cancellationToken);
        }

        PageMessage = "The communication was sent to the candidate.";
        return RedirectToPage(new { Stage, Search });
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
        string? validationError = ValidateNotificationMessage(
            RejectionMessage.NotificationMessage);
        if (validationError is not null)
        {
            PageError = validationError;
            return RedirectToPage(new { Stage = "shortlist", Search });
        }

        ShortlistResult result = await TutorApplicationReview.RejectShortlistedAsync(
            _context,
            tutorId,
            InterviewPreparation.Notes,
            cancellationToken,
            _currentUserService?.GetRequiredUser().BcUserId,
            RejectionMessage.NotificationMessage);

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

    public async Task<IActionResult> OnPostSaveInterviewNotesAsync(
        int tutorId,
        CancellationToken cancellationToken)
    {
        OpenInterviewRoomCandidateId = tutorId;
        string normalizedNotes = InterviewNotes?.Trim() ?? string.Empty;

        if (normalizedNotes.Length > 4000)
        {
            PageError = "Interview notes cannot exceed 4,000 characters.";
            return RedirectToPage(new { Stage = "interview", Search });
        }

        Tutor? candidate = await _context.Tutors.SingleOrDefaultAsync(
            tutor => tutor.TutorId == tutorId &&
                tutor.Status == TutorStatus.Pending &&
                tutor.ApplicationStage == TutorApplicationStage.Interview,
            cancellationToken);

        if (candidate is null)
        {
            PageError = "This candidate is no longer in the interview stage.";
            return RedirectToPage(new { Stage = "interview", Search });
        }

        candidate.InterviewNotes = string.IsNullOrWhiteSpace(normalizedNotes)
            ? null
            : normalizedNotes;
        await _context.SaveChangesAsync(cancellationToken);
        InterviewRoomMessage = "Interview notes saved.";

        return RedirectToPage(new { Stage = "interview", Search });
    }

    public async Task<IActionResult> OnPostMoveToPlacementAsync(
        int tutorId,
        CancellationToken cancellationToken)
    {
        ShortlistResult result = await TutorApplicationReview
            .MoveInterviewToPlacementAsync(
                _context,
                tutorId,
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

        return RedirectToPage(new { Stage = "interview", Search });
    }

    public async Task<IActionResult> OnPostRejectInterviewedAsync(
        int tutorId,
        CancellationToken cancellationToken)
    {
        string? validationError = ValidateNotificationMessage(
            RejectionMessage.NotificationMessage);
        if (validationError is not null)
        {
            PageError = validationError;
            return RedirectToPage(new { Stage = "interview", Search });
        }

        ShortlistResult result = await TutorApplicationReview
            .RejectInterviewedAsync(
                _context,
                tutorId,
                null,
                cancellationToken,
                _currentUserService?.GetRequiredUser().BcUserId,
                RejectionMessage.NotificationMessage);

        if (result.Succeeded)
        {
            PageMessage = result.Message;
        }
        else
        {
            PageError = result.Message;
        }

        return RedirectToPage(new { Stage = "interview", Search });
    }

    private async Task<CommunicationRecipient?>
        GetCommunicationRecipientAsync(
            int tutorId,
            CancellationToken cancellationToken) =>
        await _context.Tutors
            .AsNoTracking()
            .Where(tutor => tutor.TutorId == tutorId)
            .Select(tutor => new CommunicationRecipient(
                tutor.BcUserId,
                tutor.BcUser.Email,
                string.IsNullOrWhiteSpace(tutor.BcUser.DisplayName)
                    ? tutor.BcUser.PersonnelNumber
                    : tutor.BcUser.DisplayName))
            .SingleOrDefaultAsync(cancellationToken);

    private static string? ValidateComposedMessage(
        ApplicationMessageInput message,
        bool requireNotificationMessage)
    {
        string subject = message.Subject?.Trim() ?? string.Empty;
        string body = message.EmailBody?.Trim() ?? string.Empty;
        string notification = message.NotificationMessage?.Trim() ??
            string.Empty;

        if (subject.Length is < 1 or > 200)
        {
            return "Enter an email subject of no more than 200 characters.";
        }

        if (body.Length is < 1 or > 5000)
        {
            return "Enter an email message of no more than 5,000 characters.";
        }

        if (requireNotificationMessage &&
            (notification.Length is < 1 or > 1000))
        {
            return "Enter a notification message of no more than 1,000 characters.";
        }

        return null;
    }

    private static string? ValidateNotificationMessage(string? message)
    {
        string notification = message?.Trim() ?? string.Empty;
        return notification.Length is < 1 or > 1000
            ? "Enter a notification message of no more than 1,000 characters."
            : null;
    }

    private sealed record CommunicationRecipient(
        int BcUserId,
        string? Email,
        string DisplayName);

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

    public static string GetDocumentTypeLabel(TutorDocumentType type) =>
        type switch
        {
            TutorDocumentType.AcademicTranscript => "Academic transcript",
            TutorDocumentType.ExternalCertificate => "Additional certificate",
            _ => type.ToString()
        };

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

        public InterviewPreparationDetails ToDetails() => new(
            Notes,
            ScheduledDate,
            ScheduledTime,
            DurationMinutes,
            LocationOrMeetingLink);
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
        public string? InterviewNotes { get; init; }
        public DateTime? InterviewScheduledAt { get; init; }
        public int? InterviewDurationMinutes { get; init; }
        public string? InterviewLocation { get; init; }
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

        public bool HasInterviewMeetingLink => Uri.TryCreate(
                InterviewLocation,
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
