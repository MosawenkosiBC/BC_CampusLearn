using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
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

    public string TutorName { get; private set; } = "Not available";

    public string TutorEmail { get; private set; } = "Not available";

    public string? TutorProfileImagePath { get; private set; }

    public string? LatestStatusReason { get; private set; }

    public string? LatestStatusReasonTitle { get; private set; }

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
            .Include(booking => booking.SessionReviews)
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

    public async Task<IActionResult> OnPostReviewAsync(
        int bookingId,
        CancellationToken cancellationToken)
    {
        CurrentUser student = _currentUserService.GetRequiredUser();
        Booking? booking = await StudentBookings(student)
            .Include(item => item.SessionReviews)
            .Include(item => item.TutorCourseModule)
                .ThenInclude(assignment => assignment.Tutor)
            .SingleOrDefaultAsync(
                item => item.BookingId == bookingId,
                cancellationToken);

        if (booking is null)
        {
            return NotFound();
        }

        string comment = ReviewComment?.Trim() ?? string.Empty;
        bool reviewExists = booking.SessionReviews.Any(review =>
            review.ReviewerBcUserId == student.BcUserId);
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
            ReviewerBcUserId = student.BcUserId,
            RevieweeBcUserId = booking.TutorCourseModule.Tutor.BcUserId,
            Rating = ReviewRating,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment,
            CreatedAt = _timeProvider.GetUtcNow()
        });
        await _context.SaveChangesAsync(cancellationToken);

        SessionActionMessage = "Your review was submitted.";
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
