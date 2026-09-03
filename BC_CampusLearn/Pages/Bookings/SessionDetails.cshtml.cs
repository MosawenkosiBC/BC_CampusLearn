using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using BC_CampusLearn.Services.Sessions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Bookings;

[Authorize]
public class SessionDetailsModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IWebHostEnvironment _environment;
    private readonly ISessionLifecycleService _lifecycleService;

    public SessionDetailsModel(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IWebHostEnvironment environment,
        ISessionLifecycleService lifecycleService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _environment = environment;
        _lifecycleService = lifecycleService;
    }

    public Booking Session { get; private set; } = null!;

    public int CurrentBcUserId { get; private set; }

    public string TutorName { get; private set; } = "Not available";

    public string TutorEmail { get; private set; } = "Not available";

    public string? TutorProfileImagePath { get; private set; }

    public string? LatestStatusReason { get; private set; }

    public string? LatestStatusReasonTitle { get; private set; }

    [BindProperty]
    public StudentEvaluationInput EvaluationInput { get; set; } = new();

    [BindProperty]
    public string? CancellationReason { get; set; }

    [TempData]
    public string? SessionActionMessage { get; set; }

    [TempData]
    public bool SessionActionError { get; set; }

    public async Task<IActionResult> OnGetAsync(
        int bookingId,
        CancellationToken cancellationToken)
    {
        await _lifecycleService.ProcessDueTransitionsAsync(cancellationToken);
        CurrentUser student = _currentUserService.GetRequiredUser();

        Booking? session = await StudentBookings(student)
            .AsNoTracking()
            .AsSplitQuery()
            .Include(booking => booking.ProgrammeModule)
            .Include(booking => booking.TutorCourseModule)
                .ThenInclude(assignment => assignment.Tutor)
                    .ThenInclude(tutor => tutor.BcUser)
            .Include(booking => booking.PreparationLinks)
            .Include(booking => booking.Documents)
            .Include(booking => booking.SessionMessages)
                .ThenInclude(message => message.Sender)
            .Include(booking => booking.StudentEvaluation)
            .Include(booking => booking.StatusHistory)
            .SingleOrDefaultAsync(
                booking => booking.BookingId == bookingId,
                cancellationToken);

        if (session is null)
        {
            return NotFound();
        }

        Session = session;
        CurrentBcUserId = student.BcUserId;
        TutorName = string.IsNullOrWhiteSpace(
            session.TutorCourseModule.Tutor.BcUser.DisplayName)
            ? session.TutorCourseModule.Tutor.BcUser.PersonnelNumber
            : session.TutorCourseModule.Tutor.BcUser.DisplayName;
        TutorEmail = string.IsNullOrWhiteSpace(
            session.TutorCourseModule.Tutor.BcUser.Email)
            ? "Not available"
            : session.TutorCourseModule.Tutor.BcUser.Email;
        TutorProfileImagePath =
            session.TutorCourseModule.Tutor.ProfileImagePath;

        BookingStatusHistory? latestReason = session.StatusHistory
            .OrderByDescending(item => item.ChangedAt)
            .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.Reason));
        LatestStatusReason = latestReason?.ReasonCode switch
        {
            SessionLifecycleService.UnreviewedReasonCode =>
                "The booking expired because the tutor did not respond before the scheduled time.",
            SessionLifecycleService.NotStartedReasonCode =>
                "The session was cancelled because the tutor did not start it within the allowed time.",
            _ => latestReason?.Reason
        };
        LatestStatusReasonTitle = latestReason?.ReasonCode switch
        {
            SessionLifecycleService.TutorDeclinedReasonCode => "Decline Reason",
            SessionLifecycleService.TutorCancelledReasonCode => "Cancel Reason",
            SessionLifecycleService.StudentCancelledReasonCode => "Cancel Reason",
            _ => null
        };

        return Page();
    }

    public async Task<IActionResult> OnPostJoinAsync(
        int bookingId,
        CancellationToken cancellationToken)
    {
        CurrentUser student = _currentUserService.GetRequiredUser();
        var session = await StudentBookings(student)
            .AsNoTracking()
            .Where(booking => booking.BookingId == bookingId)
            .Select(booking => new
            {
                booking.Status,
                booking.MeetingLink
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (session is null)
        {
            return NotFound();
        }

        if (session.Status is not (BookingStatus.Confirmed or
            BookingStatus.InProgress) ||
            string.IsNullOrWhiteSpace(session.MeetingLink))
        {
            SessionActionError = true;
            SessionActionMessage =
                "The meeting link is not available for this session.";
            return RedirectToPage(new { bookingId });
        }

        return Redirect(session.MeetingLink);
    }

    public async Task<IActionResult> OnPostCancelAsync(
        int bookingId,
        CancellationToken cancellationToken)
    {
        CurrentUser student = _currentUserService.GetRequiredUser();
        SessionLifecycleResult result =
            await _lifecycleService.CancelByStudentAsync(
                student.BcUserId,
                student.ObjectId,
                student.TenantId,
                bookingId,
                CancellationReason,
                cancellationToken);

        SessionActionError = !result.Succeeded;
        SessionActionMessage = result.Succeeded
            ? "Session cancelled."
            : result.ErrorMessage;
        return RedirectToPage(new { bookingId });
    }

    public async Task<IActionResult> OnPostReviewAsync(
        int bookingId,
        CancellationToken cancellationToken)
    {
        CurrentUser student = _currentUserService.GetRequiredUser();
        Booking? booking = await StudentBookings(student)
            .Include(item => item.StudentEvaluation)
            .SingleOrDefaultAsync(
                item => item.BookingId == bookingId,
                cancellationToken);

        if (booking is null)
        {
            return NotFound();
        }

        bool reviewExists = booking.StudentEvaluation is not null;
        if (booking.Status != BookingStatus.Completed || reviewExists)
        {
            SessionActionError = true;
            SessionActionMessage = reviewExists
                ? "You have already reviewed this session."
                : "An evaluation can only be submitted for a completed session.";
            return RedirectToPage(new { bookingId });
        }

        string[] threeWay = ["Yes", "No", "Maybe"];
        string[] agreement =
        [
            "Strongly agree", "Agree", "Neither agree nor disagree",
            "Disagree", "Strongly disagree"
        ];
        string[] tutoringModes = ["Online", "Face-to-face"];
        string[] helpOptions =
        [
            "Test Preparation", "Explanation of Content",
            "Understanding the Concept", "Memory Techniques",
            "Problem Solving Technique"
        ];
        bool selectionsAreValid =
            tutoringModes.Contains(EvaluationInput.TutoringMode) &&
            threeWay.Contains(EvaluationInput.TutorResponse) &&
            threeWay.Contains(EvaluationInput.TutorInterest) &&
            agreement.Contains(EvaluationInput.TutorFriendliness) &&
            agreement.Contains(EvaluationInput.TutorExplanation) &&
            agreement.Contains(EvaluationInput.TutorParticipation) &&
            threeWay.Contains(EvaluationInput.TutorPunctuality) &&
            agreement.Contains(EvaluationInput.TutorAdvice) &&
            helpOptions.Contains(EvaluationInput.TutorHelp) &&
            threeWay.Contains(EvaluationInput.TutoringService) &&
            !string.IsNullOrWhiteSpace(EvaluationInput.PlatformExperience) &&
            !string.IsNullOrWhiteSpace(EvaluationInput.TutorTopic) &&
            !string.IsNullOrWhiteSpace(EvaluationInput.ImproveBCProgramme);
        if (!ModelState.IsValid || !selectionsAreValid)
        {
            SessionActionError = true;
            SessionActionMessage = "Complete all required evaluation questions.";
            return RedirectToPage(new { bookingId });
        }

        _context.StudentEvaluations.Add(new StudentEvaluation
        {
            BookingId = bookingId,
            TutoringMode = EvaluationInput.TutoringMode,
            PlatformExperience = EvaluationInput.PlatformExperience.Trim(),
            ModeRating = EvaluationInput.ModeRating!.Value,
            TutorResponse = EvaluationInput.TutorResponse,
            TutorInterest = EvaluationInput.TutorInterest,
            TutorFriendliness = EvaluationInput.TutorFriendliness,
            TutorExplanation = EvaluationInput.TutorExplanation,
            TutorParticipation = EvaluationInput.TutorParticipation,
            TutorPunctuality = EvaluationInput.TutorPunctuality,
            TutorAdvice = EvaluationInput.TutorAdvice,
            TutorHelp = EvaluationInput.TutorHelp,
            TutorTopic = EvaluationInput.TutorTopic.Trim(),
            TutoringService = EvaluationInput.TutoringService,
            ImproveBCProgramme = EvaluationInput.ImproveBCProgramme.Trim(),
            PlatformRating = EvaluationInput.PlatformRating!.Value
        });
        await _context.SaveChangesAsync(cancellationToken);

        SessionActionMessage = "Your tutor evaluation was submitted.";
        return RedirectToPage(new { bookingId });
    }

    public async Task<IActionResult> OnGetDocumentAsync(
        int bookingId,
        int documentId,
        CancellationToken cancellationToken)
    {
        CurrentUser student = _currentUserService.GetRequiredUser();
        BookingDocument? document = await _context.BookingDocuments
            .AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.BookingDocumentId == documentId &&
                item.BookingId == bookingId &&
                (item.Booking.StudentBcUserId == student.BcUserId ||
                 (item.Booking.StudentObjectId == student.ObjectId &&
                  item.Booking.StudentTenantId == student.TenantId)),
                cancellationToken);

        if (document is null)
        {
            return NotFound();
        }

        string documentRoot = Path.GetFullPath(Path.Combine(
            _environment.ContentRootPath,
            "App_Data",
            "booking-documents"));
        string fullPath = Path.GetFullPath(Path.Combine(
            _environment.ContentRootPath,
            document.StoragePath));
        string allowedPrefix = documentRoot.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(
                allowedPrefix,
                StringComparison.OrdinalIgnoreCase) ||
            !System.IO.File.Exists(fullPath))
        {
            return NotFound();
        }

        return new PhysicalFileResult(fullPath, document.ContentType)
        {
            FileDownloadName = document.OriginalFileName
        };
    }

    public static string FormatFileSize(long sizeBytes)
    {
        if (sizeBytes >= 1024 * 1024)
        {
            return $"{sizeBytes / (1024d * 1024d):0.#} MB";
        }

        return $"{Math.Max(1, sizeBytes / 1024d):0.#} KB";
    }

    private IQueryable<Booking> StudentBookings(CurrentUser student) =>
        _context.Bookings.Where(booking =>
            booking.StudentBcUserId == student.BcUserId ||
            (booking.StudentObjectId == student.ObjectId &&
             booking.StudentTenantId == student.TenantId));
}
