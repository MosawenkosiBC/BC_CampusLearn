using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Administrator.Admin;

public class BookingsAndSessionsModel(
    ApplicationDbContext context,
    TimeProvider? timeProvider = null) : PageModel
{
    public const int PageSize = 8;
    private static readonly string[] ValidQueues = ["all", "admin", "head"];
    private static readonly string[] ValidApprovalFilters =
        ["all", "awaiting", "pending", "approved"];
    private static readonly string[] ValidPeriods =
        ["month", "week", "day", "custom"];
    private static readonly TimeSpan CampusOffset = TimeSpan.FromHours(2);
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    [BindProperty(SupportsGet = true)]
    public string Queue { get; set; } = "all";

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string Approval { get; set; } = "all";

    [BindProperty(SupportsGet = true)]
    public string Period { get; set; } = "month";

    [BindProperty(SupportsGet = true)]
    public DateOnly? From { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? To { get; set; }

    [BindProperty(SupportsGet = true)]
    public int SessionPage { get; set; } = 1;

    public int CompletedSessionCount { get; private set; }
    public int RequireAdminReviewCount { get; private set; }
    public int RequireTutorHeadReviewCount { get; private set; }
    public int FilteredSessionCount { get; private set; }
    public int TotalPages { get; private set; }
    public int DisplayedSessionCount => Sessions.Count;
    public string PeriodLabel { get; private set; } = string.Empty;
    public IReadOnlyList<CompletedSessionItem> Sessions { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Queue = ValidQueues.Contains(Queue, StringComparer.OrdinalIgnoreCase)
            ? Queue.ToLowerInvariant()
            : "all";
        Approval = ValidApprovalFilters.Contains(Approval, StringComparer.OrdinalIgnoreCase)
            ? Approval.ToLowerInvariant()
            : "all";
        Period = ValidPeriods.Contains(Period, StringComparer.OrdinalIgnoreCase)
            ? Period.ToLowerInvariant()
            : "month";
        Search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim();

        (DateTimeOffset periodStart, DateTimeOffset periodEnd) = ResolvePeriod();

        IQueryable<Booking> completed = context.Bookings
            .AsNoTracking()
            .Where(booking =>
                booking.Status == BookingStatus.Completed &&
                booking.ScheduledStartTime >= periodStart &&
                booking.ScheduledStartTime < periodEnd);

        CompletedSessionCount = await completed.CountAsync(cancellationToken);
        RequireAdminReviewCount = await completed.CountAsync(booking =>
            booking.AdminSessionReview == null,
            cancellationToken);
        RequireTutorHeadReviewCount = await completed.CountAsync(booking =>
            !booking.SessionReviews.Any(review =>
                review.Reviewer.Role == BcUserRole.HeadOfTutors),
            cancellationToken);

        IQueryable<Booking> filtered = completed;
        if (Queue == "admin")
        {
            filtered = filtered.Where(booking => booking.AdminSessionReview == null);
        }
        else if (Queue == "head")
        {
            filtered = filtered.Where(booking =>
                !booking.SessionReviews.Any(review =>
                    review.Reviewer.Role == BcUserRole.HeadOfTutors));
        }

        if (Search is not null)
        {
            string search = Search;
            filtered = filtered.Where(booking =>
                booking.StudentName.Contains(search) ||
                booking.Location.Contains(search) ||
                booking.TutorCourseModule.Tutor.BcUser.DisplayName.Contains(search) ||
                booking.TutorCourseModule.Tutor.BcUser.PersonnelNumber.Contains(search));
        }

        filtered = Approval switch
        {
            "awaiting" => filtered.Where(booking =>
                booking.StudentEvaluation == null ||
                booking.TutorEvaluation == null),
            "pending" => filtered.Where(booking =>
                booking.StudentEvaluation != null &&
                booking.TutorEvaluation != null &&
                (booking.AdminSessionReview == null ||
                 !booking.AdminSessionReview.AllReviewsSubmitted ||
                 !booking.AdminSessionReview.HeadConfirmedSession ||
                 !booking.AdminSessionReview.HeadConfirmedQuality ||
                 !booking.AdminSessionReview.ConcernsResolvedOrDocumented ||
                 !booking.AdminSessionReview.EvidenceSupportsApproval)),
            "approved" => filtered.Where(booking =>
                booking.AdminSessionReview != null &&
                booking.AdminSessionReview.AllReviewsSubmitted &&
                booking.AdminSessionReview.HeadConfirmedSession &&
                booking.AdminSessionReview.HeadConfirmedQuality &&
                booking.AdminSessionReview.ConcernsResolvedOrDocumented &&
                booking.AdminSessionReview.EvidenceSupportsApproval),
            _ => filtered
        };

        FilteredSessionCount = await filtered.CountAsync(cancellationToken);
        TotalPages = Math.Max(1,
            (int)Math.Ceiling(FilteredSessionCount / (double)PageSize));
        SessionPage = Math.Clamp(SessionPage, 1, TotalPages);

        Sessions = await filtered
            .OrderByDescending(booking => booking.CompletedAt ?? booking.ScheduledStartTime)
            .ThenByDescending(booking => booking.BookingId)
            .Skip((SessionPage - 1) * PageSize)
            .Take(PageSize)
            .Select(booking => new CompletedSessionItem(
                booking.BookingId,
                booking.TutorCourseModule.Tutor.BcUser.DisplayName,
                booking.TutorCourseModule.Tutor.BcUser.PersonnelNumber,
                booking.StudentName,
                booking.Location,
                booking.ScheduledStartTime,
                booking.TutorEvaluation != null,
                booking.StudentEvaluation != null,
                booking.SessionReviews.Any(review =>
                    review.Reviewer.Role == BcUserRole.HeadOfTutors),
                booking.AdminSessionReview == null
                    ? null
                    : booking.AdminSessionReview.AllReviewsSubmitted &&
                      booking.AdminSessionReview.HeadConfirmedSession &&
                      booking.AdminSessionReview.HeadConfirmedQuality &&
                      booking.AdminSessionReview.ConcernsResolvedOrDocumented &&
                      booking.AdminSessionReview.EvidenceSupportsApproval))
            .ToListAsync(cancellationToken);
    }

    private (DateTimeOffset Start, DateTimeOffset End) ResolvePeriod()
    {
        DateOnly today = DateOnly.FromDateTime(
            _timeProvider.GetUtcNow().ToOffset(CampusOffset).Date);
        DateOnly start;
        DateOnly end;

        switch (Period)
        {
            case "day":
                start = today;
                end = today;
                PeriodLabel = today.ToString("dd MMMM yyyy");
                break;
            case "week":
                int daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
                start = today.AddDays(-daysSinceMonday);
                end = start.AddDays(6);
                PeriodLabel = $"{start:dd MMM} – {end:dd MMM yyyy}";
                break;
            case "custom" when From.HasValue || To.HasValue:
                start = From ?? To!.Value;
                end = To ?? From!.Value;
                if (end < start)
                {
                    (start, end) = (end, start);
                }
                From = start;
                To = end;
                PeriodLabel = start == end
                    ? start.ToString("dd MMMM yyyy")
                    : $"{start:dd MMM yyyy} – {end:dd MMM yyyy}";
                break;
            default:
                Period = "month";
                start = new DateOnly(today.Year, today.Month, 1);
                end = start.AddMonths(1).AddDays(-1);
                PeriodLabel = start.ToString("MMMM yyyy");
                break;
        }

        return (
            new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue), CampusOffset),
            new DateTimeOffset(end.AddDays(1).ToDateTime(TimeOnly.MinValue), CampusOffset));
    }

    public sealed record CompletedSessionItem(
        int BookingId,
        string TutorName,
        string TutorPersonnelNumber,
        string StudentName,
        string Location,
        DateTimeOffset SessionDate,
        bool HasTutorReview,
        bool HasStudentReview,
        bool HasTutorHeadReview,
        bool? IsAdminApproved)
    {
        public string TutorDisplayName => string.IsNullOrWhiteSpace(TutorName)
            ? TutorPersonnelNumber
            : TutorName;

        public string AdminApprovalLabel => IsAdminApproved == true
            ? "Approved"
            : !HasTutorReview || !HasStudentReview
                ? "Awaiting reviews"
                : "Pending approval";

        public string AdminApprovalCssClass => IsAdminApproved == true
            ? "is-approved"
            : !HasTutorReview || !HasStudentReview
                ? "is-waiting"
                : "is-pending";
    }
}
