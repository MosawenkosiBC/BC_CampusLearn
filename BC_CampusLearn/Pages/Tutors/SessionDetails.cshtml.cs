using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BC_CampusLearn.Services.Sessions;

namespace BC_CampusLearn.Pages.Tutors;

[Authorize]
public class SessionDetailsModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IWebHostEnvironment _environment;
    private readonly ISessionLifecycleService _lifecycleService;
    private readonly TimeProvider _timeProvider;

    public SessionDetailsModel(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IWebHostEnvironment environment,
        ISessionLifecycleService lifecycleService,
        TimeProvider timeProvider)
    {
        _context = context;
        _currentUserService = currentUserService;
        _environment = environment;
        _lifecycleService = lifecycleService;
        _timeProvider = timeProvider;
    }

    public Booking Session { get; private set; } = null!;

    public int CurrentBcUserId { get; private set; }

    public string TutorEmail { get; private set; } = "Not available";

    public bool CanStartSession { get; private set; }

    public string? LatestStatusReason { get; private set; }

    public string? LatestStatusReasonTitle { get; private set; }

    public bool LatestStatusReasonIsUnreviewedWarning { get; private set; }

    public string SessionStartRemainingText { get; private set; } = string.Empty;

    [BindProperty]
    public string? MeetingLink { get; set; }

    [TempData]
    public string? MeetingLinkMessage { get; set; }

    [TempData]
    public bool MeetingLinkError { get; set; }

    [BindProperty]
    public string? DeclineReason { get; set; }

    [BindProperty]
    public bool ReopenAvailability { get; set; }

    [BindProperty]
    public byte ReviewRating { get; set; }

    [BindProperty]
    public string? ReviewComment { get; set; }

    [TempData]
    public string? SessionActionMessage { get; set; }

    [TempData]
    public bool SessionActionError { get; set; }

    public async Task<IActionResult> OnGetAsync(
        int bookingId,
        CancellationToken cancellationToken)
    {
        await _lifecycleService.ProcessDueTransitionsAsync(cancellationToken);
        int? tutorId = await GetCurrentTutorIdAsync(cancellationToken);
        if (!tutorId.HasValue)
        {
            return Forbid();
        }

        Booking? session = await _context.Bookings
            .AsNoTracking()
            .AsSplitQuery()
            .Include(booking => booking.ProgrammeModule)
            .Include(booking => booking.PreparationLinks)
            .Include(booking => booking.Documents)
            .Include(booking => booking.SessionExecution)
            .Include(booking => booking.StatusHistory)
            .Include(booking => booking.SessionMessages)
                .ThenInclude(message => message.Sender)
            .Include(booking => booking.SessionReviews)
            .SingleOrDefaultAsync(booking =>
                booking.BookingId == bookingId &&
                booking.TutorId == tutorId.Value,
                cancellationToken);

        if (session is null)
        {
            return NotFound();
        }

        Session = session;
        CurrentUser currentUser = _currentUserService.GetRequiredUser();
        CurrentBcUserId = currentUser.BcUserId;
        TutorEmail = string.IsNullOrWhiteSpace(currentUser.Email)
            ? "Not available"
            : currentUser.Email;
        MeetingLink = session.MeetingLink;
        DateTimeOffset now = _timeProvider.GetUtcNow();
        SessionStartRemainingText = FormatTimeUntilStart(
            session.ScheduledStartTime - now);
        CanStartSession = session.Status == BookingStatus.Confirmed &&
            now >= session.ScheduledStartTime.Subtract(
                SessionSchedulingRules.EarlyStartWindow) &&
            now < session.ScheduledStartTime.Add(
                SessionSchedulingRules.LateStartWindow);
        BookingStatusHistory? latestStatusReason = session.StatusHistory
            .OrderByDescending(item => item.ChangedAt)
            .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.Reason));
        LatestStatusReasonIsUnreviewedWarning =
            latestStatusReason?.ReasonCode == SessionLifecycleService.UnreviewedReasonCode;
        LatestStatusReason = LatestStatusReasonIsUnreviewedWarning
            ? SessionLifecycleService.UnreviewedWarningMessage
            : latestStatusReason?.ReasonCode switch
        {
            SessionLifecycleService.NotStartedReasonCode =>
                SessionLifecycleService.NotStartedWarningMessage,
            _ => latestStatusReason?.Reason
        };
        LatestStatusReasonTitle = latestStatusReason?.ReasonCode switch
        {
            SessionLifecycleService.TutorDeclinedReasonCode => "Decline Reason",
            SessionLifecycleService.TutorCancelledReasonCode => "Cancel Reason",
            _ => null
        };
        return Page();
    }

    private static string FormatTimeUntilStart(TimeSpan remaining)
    {
        if (remaining <= TimeSpan.Zero)
        {
            return "Starts now";
        }

        int totalHours = (int)remaining.TotalHours;
        return totalHours > 0
            ? $"{totalHours}h {remaining.Minutes}m {remaining.Seconds}s Left"
            : $"{remaining.Minutes}m {remaining.Seconds}s Left";
    }

    public async Task<IActionResult> OnPostConfirmAsync(
        int bookingId,
        CancellationToken cancellationToken)
    {
        CurrentUser currentUser = _currentUserService.GetRequiredUser();
        int? tutorId = await GetCurrentTutorIdAsync(cancellationToken);
        if (!tutorId.HasValue)
        {
            return Forbid();
        }

        SessionLifecycleResult result = await _lifecycleService.ConfirmAsync(
            tutorId.Value,
            currentUser.BcUserId,
            bookingId,
            MeetingLink,
            cancellationToken);
        SessionActionError = !result.Succeeded;
        SessionActionMessage = result.Succeeded
            ? "Session confirmed and meeting link saved."
            : result.ErrorMessage;
        return RedirectToPage(new { bookingId });
    }

    public async Task<IActionResult> OnPostDeclineAsync(
        int bookingId,
        CancellationToken cancellationToken)
    {
        CurrentUser currentUser = _currentUserService.GetRequiredUser();
        int? tutorId = await GetCurrentTutorIdAsync(cancellationToken);
        if (!tutorId.HasValue)
        {
            return Forbid();
        }

        SessionLifecycleResult result = await _lifecycleService.DeclineAsync(
            tutorId.Value,
            currentUser.BcUserId,
            bookingId,
            reason: null,
            reopenAvailability: false,
            cancellationToken);
        SessionActionError = !result.Succeeded;
        SessionActionMessage = result.Succeeded
            ? "Booking declined."
            : result.ErrorMessage;
        return RedirectToPage(new { bookingId });
    }

    public async Task<IActionResult> OnPostCancelAsync(
        int bookingId,
        CancellationToken cancellationToken)
    {
        CurrentUser currentUser = _currentUserService.GetRequiredUser();
        int? tutorId = await GetCurrentTutorIdAsync(cancellationToken);
        if (!tutorId.HasValue)
        {
            return Forbid();
        }

        SessionLifecycleResult result = await _lifecycleService.DeclineAsync(
            tutorId.Value,
            currentUser.BcUserId,
            bookingId,
            DeclineReason,
            ReopenAvailability,
            cancellationToken);
        SessionActionError = !result.Succeeded;
        SessionActionMessage = result.Succeeded
            ? "Session cancelled."
            : result.ErrorMessage;
        return RedirectToPage(new { bookingId });
    }

    public async Task<IActionResult> OnPostStartAsync(
        int bookingId,
        CancellationToken cancellationToken)
    {
        CurrentUser currentUser = _currentUserService.GetRequiredUser();
        int? tutorId = await GetCurrentTutorIdAsync(cancellationToken);
        if (!tutorId.HasValue)
        {
            return Forbid();
        }

        SessionLifecycleResult result = await _lifecycleService.StartAsync(
            tutorId.Value,
            currentUser.BcUserId,
            bookingId,
            SessionStartSource.Manual,
            cancellationToken);
        SessionActionError = !result.Succeeded;
        SessionActionMessage = result.Succeeded
            ? "Session started."
            : result.ErrorMessage;
        return RedirectToPage(new { bookingId });
    }

    public async Task<IActionResult> OnPostJoinAsync(
        int bookingId,
        CancellationToken cancellationToken)
    {
        CurrentUser currentUser = _currentUserService.GetRequiredUser();
        int? tutorId = await GetCurrentTutorIdAsync(cancellationToken);
        if (!tutorId.HasValue)
        {
            return Forbid();
        }

        string? link = await _context.Bookings
            .AsNoTracking()
            .Where(booking =>
                booking.BookingId == bookingId &&
                booking.TutorId == tutorId.Value)
            .Select(booking => booking.MeetingLink)
            .SingleOrDefaultAsync(cancellationToken);
        SessionLifecycleResult result = await _lifecycleService.StartAsync(
            tutorId.Value,
            currentUser.BcUserId,
            bookingId,
            SessionStartSource.JoinMeeting,
            cancellationToken);
        if (!result.Succeeded || string.IsNullOrWhiteSpace(link))
        {
            SessionActionError = true;
            SessionActionMessage = result.ErrorMessage ??
                "The meeting link is unavailable.";
            return RedirectToPage(new { bookingId });
        }

        return Redirect(link);
    }

    public async Task<IActionResult> OnPostReviewAsync(
        int bookingId,
        CancellationToken cancellationToken)
    {
        CurrentUser currentUser = _currentUserService.GetRequiredUser();
        int? tutorId = await GetCurrentTutorIdAsync(cancellationToken);
        if (!tutorId.HasValue)
        {
            return Forbid();
        }

        Booking? booking = await _context.Bookings
            .Include(item => item.SessionReviews)
            .SingleOrDefaultAsync(item =>
                item.BookingId == bookingId &&
                item.TutorId == tutorId.Value,
                cancellationToken);
        if (booking is null)
        {
            return NotFound();
        }

        string comment = ReviewComment?.Trim() ?? string.Empty;
        bool reviewExists = booking.SessionReviews.Any(review =>
            review.ReviewerBcUserId == currentUser.BcUserId);
        if (booking.Status != BookingStatus.Completed ||
            ReviewRating is < 1 or > 5 ||
            comment.Length > 2000 ||
            reviewExists)
        {
            SessionActionError = true;
            SessionActionMessage = reviewExists
                ? "You have already reviewed this session."
                : "A review requires a completed session and a rating from 1 to 5.";
            return RedirectToPage(new { bookingId });
        }

        _context.SessionReviews.Add(new SessionReview
        {
            BookingId = bookingId,
            ReviewerBcUserId = currentUser.BcUserId,
            RevieweeBcUserId = booking.StudentBcUserId,
            Rating = ReviewRating,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment,
            CreatedAt = _timeProvider.GetUtcNow()
        });
        await _context.SaveChangesAsync(cancellationToken);
        SessionActionMessage = "Your review was submitted.";
        return RedirectToPage(new { bookingId });
    }

    public async Task<IActionResult> OnPostSaveMeetingLinkAsync(
        int bookingId,
        CancellationToken cancellationToken)
    {
        int? tutorId = await GetCurrentTutorIdAsync(cancellationToken);
        if (!tutorId.HasValue)
        {
            return Forbid();
        }

        Booking? booking = await _context.Bookings
            .SingleOrDefaultAsync(item =>
                item.BookingId == bookingId &&
                item.TutorId == tutorId.Value,
                cancellationToken);
        if (booking is null)
        {
            return NotFound();
        }

        if (booking.Status != BookingStatus.Confirmed)
        {
            MeetingLinkError = true;
            MeetingLinkMessage =
                "Confirm the session before adding a meeting link.";
            return RedirectToPage(new { bookingId });
        }

        string link = MeetingLink?.Trim() ?? string.Empty;
        if (link.Length == 0 ||
            link.Length > 2048 ||
            !Uri.TryCreate(link, UriKind.Absolute, out Uri? meetingUri) ||
            (meetingUri.Scheme != Uri.UriSchemeHttp &&
             meetingUri.Scheme != Uri.UriSchemeHttps))
        {
            MeetingLinkError = true;
            MeetingLinkMessage =
                "Enter a valid HTTP or HTTPS meeting link.";
            return RedirectToPage(new { bookingId });
        }

        booking.MeetingLink = link;
        await _context.SaveChangesAsync(cancellationToken);

        MeetingLinkMessage = "Meeting link saved.";
        return RedirectToPage(new { bookingId });
    }

    public async Task<IActionResult> OnGetDocumentAsync(
        int bookingId,
        int documentId,
        CancellationToken cancellationToken)
    {
        int? tutorId = await GetCurrentTutorIdAsync(cancellationToken);
        if (!tutorId.HasValue)
        {
            return Forbid();
        }

        BookingDocument? document = await _context.BookingDocuments
            .AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.BookingDocumentId == documentId &&
                item.BookingId == bookingId &&
                item.Booking.TutorId == tutorId.Value,
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

    private async Task<int?> GetCurrentTutorIdAsync(
        CancellationToken cancellationToken)
    {
        CurrentUser currentUser = _currentUserService.GetRequiredUser();

        return await _context.Tutors
            .AsNoTracking()
            .Where(tutor =>
                tutor.BcUserId == currentUser.BcUserId &&
                tutor.IsActive)
            .Select(tutor => (int?)tutor.TutorId)
            .SingleOrDefaultAsync(cancellationToken);
    }
}
