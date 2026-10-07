using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Administrator.Admin;

public class BookingsAndSessionsModel(
    ApplicationDbContext context,
    TimeProvider? timeProvider = null,
    ICurrentUserService? currentUserService = null) : PageModel
{
    public const int PageSize = 8;
    private static readonly string[] ValidApprovalFilters =
        ["all", "pending", "approved", "rejected"];
    private static readonly string[] ValidStatFilters =
        ["all", "carried-over", "flagged", "tutor-head-rejected", "awaiting"];
    private static readonly string[] ValidPeriods =
        ["month", "week", "day", "custom"];
    private static readonly TimeSpan CampusOffset = TimeSpan.FromHours(2);
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private DateTimeOffset _currentReviewPeriodStart;
    private DateTimeOffset _currentReviewPeriodEnd;
    private bool _currentReviewDeadlinePassed;

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string Approval { get; set; } = "all";

    [BindProperty(SupportsGet = true)]
    public string Stat { get; set; } = "all";

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
    public bool IsSuperAdmin { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        IsSuperAdmin = currentUserService?.GetRequiredUser().Role ==
            BcUserRole.SuperAdmin;
        if (IsSuperAdmin)
        {
            Stat = "all";
        }
        Approval = ValidApprovalFilters.Contains(Approval, StringComparer.OrdinalIgnoreCase)
            ? Approval.ToLowerInvariant()
            : "all";
        Stat = ValidStatFilters.Contains(Stat, StringComparer.OrdinalIgnoreCase)
            ? Stat.ToLowerInvariant()
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
                    settings.AdminSessionReviewPeriodStartDate,
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

        if (IsSuperAdmin)
        {
            reviewedSessions = reviewedSessions.Where(booking =>
                booking.AdminSessionReview != null &&
                booking.AdminSessionReview.EvidenceSupportsApproval);
        }

        int carriedOver = await reviewedSessions.CountAsync(booking =>
            (booking.CompletedAt ?? booking.ScheduledStartTime) <
                _currentReviewPeriodStart &&
            booking.AdminSessionReview == null &&
            booking.SessionReviews
                .Where(review =>
                    review.Reviewer.Role == BcUserRole.HeadOfTutors)
                .OrderByDescending(review => review.CreatedAt)
                .Select(review => review.Decision)
                .FirstOrDefault() != "Reject",
            cancellationToken);
        int awaitingAdminReview = await reviewedSessions.CountAsync(booking =>
            (booking.CompletedAt ?? booking.ScheduledStartTime) <
                _currentReviewPeriodEnd &&
            booking.AdminSessionReview == null &&
            (booking.SessionReviews
                 .Where(review =>
                     review.Reviewer.Role == BcUserRole.HeadOfTutors)
                 .OrderByDescending(review => review.CreatedAt)
                 .Select(review => review.Decision)
                 .FirstOrDefault() != "Reject" ||
             ((booking.CompletedAt ?? booking.ScheduledStartTime) >=
                  _currentReviewPeriodStart &&
              !_currentReviewDeadlinePassed)),
            cancellationToken);
        int flaggedConcerns = await reviewedSessions.CountAsync(booking =>
            (booking.CompletedAt ?? booking.ScheduledStartTime) <
                _currentReviewPeriodEnd &&
            ((booking.CompletedAt ?? booking.ScheduledStartTime) >=
                _currentReviewPeriodStart ||
             (booking.AdminSessionReview == null &&
              booking.SessionReviews
                  .Where(review =>
                      review.Reviewer.Role == BcUserRole.HeadOfTutors)
                  .OrderByDescending(review => review.CreatedAt)
                  .Select(review => review.Decision)
                  .FirstOrDefault() != "Reject")) &&
            (booking.AdminSessionReview == null ||
             !booking.AdminSessionReview.EvidenceSupportsApproval) &&
            booking.SessionReviews
                .Where(review =>
                    review.Reviewer.Role == BcUserRole.HeadOfTutors)
                .OrderByDescending(review => review.CreatedAt)
                .Select(review => review.ConcernLevel)
                .FirstOrDefault() == "Concerns",
            cancellationToken);
        int rejectedByTutorHead = await reviewedSessions.CountAsync(booking =>
            (booking.CompletedAt ?? booking.ScheduledStartTime) <
                _currentReviewPeriodEnd &&
            ((booking.CompletedAt ?? booking.ScheduledStartTime) >=
                _currentReviewPeriodStart ||
             (booking.AdminSessionReview == null &&
              booking.SessionReviews
                  .Where(review =>
                      review.Reviewer.Role == BcUserRole.HeadOfTutors)
                  .OrderByDescending(review => review.CreatedAt)
                  .Select(review => review.Decision)
                  .FirstOrDefault() != "Reject")) &&
            booking.SessionReviews
                .Where(review =>
                    review.Reviewer.Role == BcUserRole.HeadOfTutors)
                .OrderByDescending(review => review.CreatedAt)
                .Select(review => review.Decision)
                .FirstOrDefault() == "Reject",
            cancellationToken);
        ReviewStats = new AdminReviewStats(
            carriedOver,
            FlaggedConcerns: flaggedConcerns,
            RejectedByTutorHead: rejectedByTutorHead,
            AwaitingAdminReview: awaitingAdminReview);

        if (Period == "month" && deadlineSettings is not null)
        {
            reviewedSessions = reviewedSessions.Where(booking =>
                (booking.CompletedAt ?? booking.ScheduledStartTime) <
                    periodEnd &&
                ((booking.CompletedAt ?? booking.ScheduledStartTime) >=
                    periodStart ||
                 (booking.AdminSessionReview == null &&
                  booking.SessionReviews
                      .Where(review =>
                          review.Reviewer.Role == BcUserRole.HeadOfTutors)
                      .OrderByDescending(review => review.CreatedAt)
                      .Select(review => review.Decision)
                      .FirstOrDefault() != "Reject")));
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

        filtered = Stat switch
        {
            "carried-over" => filtered.Where(booking =>
                (booking.CompletedAt ?? booking.ScheduledStartTime) <
                    _currentReviewPeriodStart &&
                booking.AdminSessionReview == null &&
                booking.SessionReviews
                    .Where(review =>
                        review.Reviewer.Role == BcUserRole.HeadOfTutors)
                    .OrderByDescending(review => review.CreatedAt)
                    .Select(review => review.Decision)
                    .FirstOrDefault() != "Reject"),
            "flagged" => filtered.Where(booking =>
                (booking.AdminSessionReview == null ||
                 !booking.AdminSessionReview.EvidenceSupportsApproval) &&
                booking.SessionReviews
                    .Where(review =>
                        review.Reviewer.Role == BcUserRole.HeadOfTutors)
                    .OrderByDescending(review => review.CreatedAt)
                    .Select(review => review.ConcernLevel)
                    .FirstOrDefault() == "Concerns"),
            "tutor-head-rejected" => filtered.Where(booking =>
                booking.SessionReviews
                    .Where(review =>
                        review.Reviewer.Role == BcUserRole.HeadOfTutors)
                    .OrderByDescending(review => review.CreatedAt)
                    .Select(review => review.Decision)
                    .FirstOrDefault() == "Reject"),
            "awaiting" => filtered.Where(booking =>
                booking.AdminSessionReview == null &&
                (booking.SessionReviews
                     .Where(review =>
                         review.Reviewer.Role == BcUserRole.HeadOfTutors)
                     .OrderByDescending(review => review.CreatedAt)
                     .Select(review => review.Decision)
                     .FirstOrDefault() != "Reject" ||
                 ((booking.CompletedAt ?? booking.ScheduledStartTime) >=
                      _currentReviewPeriodStart &&
                  !_currentReviewDeadlinePassed))),
            _ => filtered
        };

        if (Search is not null)
        {
            string search = Search;
            filtered = filtered.Where(booking =>
                booking.StudentName.Contains(search) ||
                booking.Location.Contains(search) ||
                booking.TutorCourseModule.Tutor.BcUser.DisplayName.Contains(search) ||
                booking.TutorCourseModule.Tutor.BcUser.PersonnelNumber.Contains(search));
        }

        filtered = IsSuperAdmin
            ? Approval switch
            {
                "pending" => filtered.Where(booking =>
                    booking.SuperAdminSessionReview == null),
                "approved" => filtered.Where(booking =>
                    booking.SuperAdminSessionReview != null &&
                    booking.SuperAdminSessionReview.IsAccepted),
                "rejected" => filtered.Where(booking =>
                    booking.SuperAdminSessionReview != null &&
                    !booking.SuperAdminSessionReview.IsAccepted),
                _ => filtered
            }
            : Approval switch
            {
            "pending" => filtered.Where(booking =>
                booking.AdminSessionReview == null &&
                (booking.SessionReviews
                     .Where(review =>
                         review.Reviewer.Role == BcUserRole.HeadOfTutors)
                     .OrderByDescending(review => review.CreatedAt)
                     .Select(review => review.Decision)
                     .FirstOrDefault() != "Reject" ||
                 ((booking.CompletedAt ?? booking.ScheduledStartTime) >=
                      _currentReviewPeriodStart &&
                  !_currentReviewDeadlinePassed))),
            "approved" => filtered.Where(booking =>
                booking.AdminSessionReview != null &&
                booking.AdminSessionReview.EvidenceSupportsApproval),
            "rejected" => filtered.Where(booking =>
                (booking.AdminSessionReview != null &&
                 !booking.AdminSessionReview.EvidenceSupportsApproval) ||
                (booking.AdminSessionReview == null &&
                 booking.SessionReviews
                     .Where(review =>
                         review.Reviewer.Role == BcUserRole.HeadOfTutors)
                     .OrderByDescending(review => review.CreatedAt)
                     .Select(review => review.Decision)
                     .FirstOrDefault() == "Reject" &&
                 ((booking.CompletedAt ?? booking.ScheduledStartTime) <
                      _currentReviewPeriodStart ||
                  (_currentReviewDeadlinePassed &&
                   (booking.CompletedAt ?? booking.ScheduledStartTime) <
                       _currentReviewPeriodEnd)))),
                _ => filtered
            };

        FilteredSessionCount = await filtered.CountAsync(cancellationToken);
        TotalPages = Math.Max(1,
            (int)Math.Ceiling(FilteredSessionCount / (double)PageSize));
        SessionPage = Math.Clamp(SessionPage, 1, TotalPages);

        Sessions = await filtered
            .OrderBy(booking => booking.AdminSessionReview != null)
            .ThenBy(booking =>
                booking.SessionReviews
                    .Where(review =>
                        review.Reviewer.Role == BcUserRole.HeadOfTutors)
                    .OrderByDescending(review => review.CreatedAt)
                    .Select(review => review.Decision)
                    .FirstOrDefault() == "Approve"
                    ? 0
                    : booking.SessionReviews
                        .Where(review =>
                            review.Reviewer.Role == BcUserRole.HeadOfTutors)
                        .OrderByDescending(review => review.CreatedAt)
                        .Select(review => review.Decision)
                        .FirstOrDefault() == "Escalate"
                        ? 2
                        : booking.SessionReviews
                            .Where(review =>
                                review.Reviewer.Role == BcUserRole.HeadOfTutors)
                            .OrderByDescending(review => review.CreatedAt)
                            .Select(review => review.Decision)
                            .FirstOrDefault() == "Reject"
                            ? 4
                            : 5)
            .ThenBy(booking =>
                booking.SessionReviews
                    .Where(review =>
                        review.Reviewer.Role == BcUserRole.HeadOfTutors)
                    .OrderByDescending(review => review.CreatedAt)
                    .Select(review => review.ConcernLevel)
                    .FirstOrDefault() == "Concerns"
                    ? 1
                    : 0)
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
                booking.SessionReviews
                    .Where(review =>
                        review.Reviewer.Role == BcUserRole.HeadOfTutors)
                    .OrderByDescending(review => review.CreatedAt)
                    .Select(review => review.Decision)
                    .FirstOrDefault(),
                (booking.AdminSessionReview == null ||
                 !booking.AdminSessionReview.EvidenceSupportsApproval) &&
                booking.SessionReviews
                    .Where(review =>
                        review.Reviewer.Role == BcUserRole.HeadOfTutors)
                    .OrderByDescending(review => review.CreatedAt)
                    .Select(review => review.ConcernLevel)
                    .FirstOrDefault() == "Concerns",
                booking.AdminSessionReview == null
                    ? null
                    : booking.AdminSessionReview.EvidenceSupportsApproval,
                booking.AdminSessionReview == null &&
                booking.SessionReviews
                    .Where(review =>
                        review.Reviewer.Role == BcUserRole.HeadOfTutors)
                    .OrderByDescending(review => review.CreatedAt)
                    .Select(review => review.Decision)
                    .FirstOrDefault() == "Reject" &&
                ((booking.CompletedAt ?? booking.ScheduledStartTime) <
                     _currentReviewPeriodStart ||
                 (_currentReviewDeadlinePassed &&
                  (booking.CompletedAt ?? booking.ScheduledStartTime) <
                      _currentReviewPeriodEnd)),
                booking.SuperAdminSessionReview == null
                    ? null
                    : booking.SuperAdminSessionReview.IsAccepted))
            .ToListAsync(cancellationToken);
    }

    private (DateTimeOffset Start, DateTimeOffset End) ResolvePeriod(
        AdminReviewDeadlineSettings? deadlineSettings)
    {
        DateOnly today = DateOnly.FromDateTime(
            _timeProvider.GetUtcNow().ToOffset(CampusOffset).Date);
        ReviewPeriodWindow? currentReviewPeriod = deadlineSettings switch
        {
            null => null,
            { IsRecurring: true } => MonthlyReviewPeriod.ResolveByCalendarMonth(
                deadlineSettings.StartDate,
                deadlineSettings.Deadline,
                deadlineSettings.UseLastDayOfMonth,
                today),
            _ => new ReviewPeriodWindow(
                deadlineSettings.StartDate,
                deadlineSettings.Deadline,
                deadlineSettings.Deadline)
        };
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
        _currentReviewDeadlinePassed = today > currentReviewPeriodEnd;
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
        string? TutorHeadDecision,
        bool HasTutorHeadConcern,
        bool? IsAdminApproved,
        bool IsTutorHeadRejectionFinal,
        bool? IsSuperAdminAccepted)
    {
        public string TutorDisplayName => string.IsNullOrWhiteSpace(TutorName)
            ? TutorPersonnelNumber
            : TutorName;

        public string AdminApprovalLabel => IsAdminApproved switch
        {
            true => "Accepted",
            false => "Rejected",
            _ when IsTutorHeadRejectionFinal => "Rejected",
            _ => "Awaiting review"
        };

        public string AdminApprovalCssClass => IsAdminApproved switch
        {
            true => "is-reviewed",
            false => "is-rejected",
            _ when IsTutorHeadRejectionFinal => "is-rejected",
            _ => "is-awaiting"
        };

        public string TutorHeadDecisionLabel => TutorHeadDecision switch
        {
            string decision when decision.Equals("Approve",
                StringComparison.OrdinalIgnoreCase) => "Approved",
            string decision when decision.Equals("Reject",
                StringComparison.OrdinalIgnoreCase) => "Rejected",
            string decision when decision.Equals("Escalate",
                StringComparison.OrdinalIgnoreCase) => "Escalated",
            _ => "Decision unavailable"
        };

        public string TutorHeadDecisionCssClass => TutorHeadDecision switch
        {
            string decision when decision.Equals("Approve",
                StringComparison.OrdinalIgnoreCase) => "is-approved",
            string decision when decision.Equals("Reject",
                StringComparison.OrdinalIgnoreCase) => "is-rejected",
            string decision when decision.Equals("Escalate",
                StringComparison.OrdinalIgnoreCase) => "is-escalated",
            _ => "is-unavailable"
        };

        public string SuperAdminDecisionLabel => IsSuperAdminAccepted switch
        {
            true => "Accepted",
            false => "Rejected",
            _ => "Awaiting review"
        };

        public string SuperAdminDecisionCssClass => IsSuperAdminAccepted switch
        {
            true => "is-reviewed",
            false => "is-rejected",
            _ => "is-awaiting"
        };
    }

    private sealed record AdminReviewDeadlineSettings(
        DateOnly StartDate,
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
