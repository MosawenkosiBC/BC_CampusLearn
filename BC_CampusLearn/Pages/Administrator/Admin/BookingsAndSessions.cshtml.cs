using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Administrator.Admin;

public class BookingsAndSessionsModel(
    ApplicationDbContext context,
    TimeProvider? timeProvider = null) : PageModel
{
    public const int PageSize = 8;
    private static readonly string[] ValidApprovalFilters =
        ["all", "pending", "approved"];
    private static readonly string[] ValidPeriods =
        ["month", "week", "day", "custom"];
    private static readonly TimeSpan CampusOffset = TimeSpan.FromHours(2);
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private DateTimeOffset _currentReviewPeriodStart;
    private DateTimeOffset _currentReviewPeriodEnd;

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

    public int FilteredSessionCount { get; private set; }
    public int TotalPages { get; private set; }
    public int DisplayedSessionCount => Sessions.Count;
    public string PeriodLabel { get; private set; } = string.Empty;
    public string CurrentReviewPeriodLabel { get; private set; } = string.Empty;
    public AdminReviewStats ReviewStats { get; private set; } =
        AdminReviewStats.Empty;
    public IReadOnlyList<CompletedSessionItem> Sessions { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Approval = ValidApprovalFilters.Contains(Approval, StringComparer.OrdinalIgnoreCase)
            ? Approval.ToLowerInvariant()
            : "all";
        Period = ValidPeriods.Contains(Period, StringComparer.OrdinalIgnoreCase)
            ? Period.ToLowerInvariant()
            : "month";
        Search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim();

        AdminReviewDeadlineSettings? deadlineSettings =
            await context.PlatformSettings
                .AsNoTracking()
                .Where(settings => settings.PlatformSettingsId ==
                    PlatformSettings.SingletonId)
                .Select(settings => new AdminReviewDeadlineSettings(
                    settings.AdminSessionReviewDeadline,
                    settings.IsAdminSessionReviewDeadlineRecurring,
                    settings.UseLastDayOfMonthForAdminSessionReviewDeadline))
                .SingleOrDefaultAsync(cancellationToken);
        (DateTimeOffset periodStart, DateTimeOffset periodEnd) =
            ResolvePeriod(deadlineSettings);

        IQueryable<Booking> reviewedSessions = context.Bookings
            .AsNoTracking()
            .Where(booking =>
                booking.Status == BookingStatus.Completed &&
                booking.StudentEvaluation != null &&
                booking.TutorEvaluation != null &&
                booking.SessionReviews.Any(review =>
                    review.Reviewer.Role == BcUserRole.HeadOfTutors));

        int carriedOver = await reviewedSessions.CountAsync(booking =>
            (booking.CompletedAt ?? booking.ScheduledStartTime) <
                _currentReviewPeriodStart &&
            booking.AdminSessionReview == null,
            cancellationToken);
        int awaitingAdminReview = await reviewedSessions.CountAsync(booking =>
            (booking.CompletedAt ?? booking.ScheduledStartTime) <
                _currentReviewPeriodEnd &&
            booking.AdminSessionReview == null,
            cancellationToken);
        ReviewStats = new AdminReviewStats(
            carriedOver,
            FlaggedConcerns: 0,
            RejectedByTutorHead: 0,
            AwaitingAdminReview: awaitingAdminReview);

        if (Period == "month" && deadlineSettings is not null)
        {
            reviewedSessions = reviewedSessions.Where(booking =>
                (booking.CompletedAt ?? booking.ScheduledStartTime) <
                    periodEnd &&
                ((booking.CompletedAt ?? booking.ScheduledStartTime) >=
                    periodStart ||
                 booking.AdminSessionReview == null));
        }
        else
        {
            reviewedSessions = reviewedSessions.Where(booking =>
                (booking.CompletedAt ?? booking.ScheduledStartTime) >=
                    periodStart &&
                (booking.CompletedAt ?? booking.ScheduledStartTime) <
                    periodEnd);
        }

        IQueryable<Booking> filtered = reviewedSessions;

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
            "pending" => filtered.Where(booking =>
                booking.AdminSessionReview == null),
            "approved" => filtered.Where(booking =>
                booking.AdminSessionReview != null),
            _ => filtered
        };

        FilteredSessionCount = await filtered.CountAsync(cancellationToken);
        TotalPages = Math.Max(1,
            (int)Math.Ceiling(FilteredSessionCount / (double)PageSize));
        SessionPage = Math.Clamp(SessionPage, 1, TotalPages);

        Sessions = await filtered
            .OrderBy(booking => booking.AdminSessionReview != null)
            .ThenByDescending(booking =>
                booking.CompletedAt ?? booking.ScheduledStartTime)
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
                    : true))
            .ToListAsync(cancellationToken);
    }

    private (DateTimeOffset Start, DateTimeOffset End) ResolvePeriod(
        AdminReviewDeadlineSettings? deadlineSettings)
    {
        DateOnly today = DateOnly.FromDateTime(
            _timeProvider.GetUtcNow().ToOffset(CampusOffset).Date);
        ReviewPeriodWindow? currentReviewPeriod = deadlineSettings is null
            ? null
            : MonthlyReviewPeriod.Resolve(
                deadlineSettings.Deadline,
                deadlineSettings.UseLastDayOfMonth,
                today);
        DateOnly currentReviewPeriodStart = currentReviewPeriod?.StartDate ??
            new DateOnly(today.Year, today.Month, 1);
        DateOnly currentReviewPeriodEnd = currentReviewPeriod?.EndDate ??
            currentReviewPeriodStart.AddMonths(1).AddDays(-1);
        CurrentReviewPeriodLabel =
            $"{currentReviewPeriodStart:dd MMM yyyy} – {currentReviewPeriodEnd:dd MMM yyyy}";
        _currentReviewPeriodStart = new DateTimeOffset(
            currentReviewPeriodStart.ToDateTime(TimeOnly.MinValue),
            CampusOffset);
        _currentReviewPeriodEnd = new DateTimeOffset(
            currentReviewPeriodEnd.AddDays(1).ToDateTime(TimeOnly.MinValue),
            CampusOffset);
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
                if (currentReviewPeriod is not null)
                {
                    start = currentReviewPeriod.StartDate;
                    end = currentReviewPeriod.EndDate;
                    PeriodLabel = CurrentReviewPeriodLabel;
                }
                else
                {
                    start = new DateOnly(today.Year, today.Month, 1);
                    end = start.AddMonths(1).AddDays(-1);
                    PeriodLabel = start.ToString("MMMM yyyy");
                }
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
            ? "Reviewed"
            : "Awaiting review";

        public string AdminApprovalCssClass => IsAdminApproved == true
            ? "is-reviewed"
            : "is-awaiting";
    }

    private sealed record AdminReviewDeadlineSettings(
        DateOnly Deadline,
        bool IsRecurring,
        bool UseLastDayOfMonth);

    public sealed record AdminReviewStats(
        int CarriedOver,
        int FlaggedConcerns,
        int RejectedByTutorHead,
        int AwaitingAdminReview)
    {
        public static AdminReviewStats Empty { get; } =
            new(0, 0, 0, 0);

        public int AwaitingAdminReviewPercentage =>
            Math.Min(100, (int)Math.Round(
                AwaitingAdminReview * 100d / PageSize,
                MidpointRounding.AwayFromZero));
    }
}
