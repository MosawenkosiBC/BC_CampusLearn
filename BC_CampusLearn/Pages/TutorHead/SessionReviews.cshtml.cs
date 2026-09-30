using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.TutorHead;

[Authorize(Roles = nameof(BcUserRole.HeadOfTutors))]
public class SessionReviewsModel(
    ApplicationDbContext context,
    TimeProvider? timeProvider = null) : PageModel
{
    private static readonly TimeSpan SouthAfricaOffset =
        TimeSpan.FromHours(2);

    [BindProperty(SupportsGet = true)]
    public string? TutorFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? StudentFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? DateFrom { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? DateTo { get; set; }

    public string? DateRangeError { get; private set; }

    public IReadOnlyList<SessionReviewListItem> Sessions { get; private set; }
        = [];

    public ReviewPeriodSummary PeriodSummary { get; private set; } =
        ReviewPeriodSummary.Empty;

    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(TutorFilter) ||
        !string.IsNullOrWhiteSpace(StudentFilter) ||
        DateFrom.HasValue ||
        DateTo.HasValue;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        TutorFilter = string.IsNullOrWhiteSpace(TutorFilter)
            ? null
            : TutorFilter.Trim();
        StudentFilter = string.IsNullOrWhiteSpace(StudentFilter)
            ? null
            : StudentFilter.Trim();

        DateTimeOffset now = (timeProvider ?? TimeProvider.System)
            .GetUtcNow()
            .ToOffset(SouthAfricaOffset);
        DateOnly today = DateOnly.FromDateTime(now.DateTime);
        ReviewPeriodDates? configuredPeriod = await context.PlatformSettings
            .AsNoTracking()
            .Where(settings => settings.PlatformSettingsId ==
                PlatformSettings.SingletonId)
            .Select(settings => new ReviewPeriodDates(
                settings.TutorHeadReviewPeriodStartDate,
                settings.TutorHeadReviewPeriodEndDate,
                settings.TutorHeadReviewDeadline))
            .SingleOrDefaultAsync(cancellationToken);
        ReviewPeriodDates period = configuredPeriod ??
            ReviewPeriodDates.ForMonth(today);
        DateTimeOffset periodStart = StartOfDay(period.StartDate);
        DateTimeOffset periodEndExclusive =
            StartOfDay(period.EndDate.AddDays(1));

        IQueryable<Booking> eligibleSessions = context.Bookings
            .AsNoTracking()
            .Where(booking =>
                booking.Status == BookingStatus.Completed &&
                booking.StudentEvaluation != null &&
                booking.TutorEvaluation != null);

        IQueryable<Booking> currentPeriodSessions = eligibleSessions.Where(
            booking =>
                (booking.CompletedAt ?? booking.ScheduledStartTime) >=
                    periodStart &&
                (booking.CompletedAt ?? booking.ScheduledStartTime) <
                    periodEndExclusive);

        int totalSessions = await currentPeriodSessions.CountAsync(
            cancellationToken);
        int reviewedSessions = await currentPeriodSessions.CountAsync(
            booking => booking.SessionReviews.Any(review =>
                review.Reviewer.Role == BcUserRole.HeadOfTutors),
            cancellationToken);
        PeriodSummary = new ReviewPeriodSummary(
            period.StartDate,
            period.EndDate,
            period.Deadline,
            totalSessions,
            totalSessions - reviewedSessions,
            reviewedSessions,
            period.Deadline.DayNumber - today.DayNumber);

        bool hasCustomDateRange = DateFrom.HasValue || DateTo.HasValue;
        IQueryable<Booking> query = hasCustomDateRange
            ? eligibleSessions
            : currentPeriodSessions;

        if (TutorFilter is not null)
        {
            string tutorFilter = TutorFilter;
            query = query.Where(booking =>
                booking.TutorCourseModule.Tutor.BcUser.DisplayName
                    .Contains(tutorFilter) ||
                booking.TutorCourseModule.Tutor.BcUser.PersonnelNumber
                    .Contains(tutorFilter));
        }

        if (StudentFilter is not null)
        {
            string studentFilter = StudentFilter;
            query = query.Where(booking =>
                booking.StudentName.Contains(studentFilter));
        }

        if (DateFrom.HasValue && DateTo.HasValue &&
            DateFrom.Value > DateTo.Value)
        {
            DateRangeError = "The end date must be on or after the start date.";
            query = query.Where(_ => false);
        }
        else
        {
            if (DateFrom.HasValue)
            {
                DateTimeOffset dateStart = StartOfDay(DateFrom.Value);
                query = query.Where(booking =>
                    (booking.CompletedAt ?? booking.ScheduledStartTime) >=
                        dateStart);
            }

            if (DateTo.HasValue)
            {
                DateTimeOffset dateEnd = StartOfDay(
                    DateTo.Value.AddDays(1));
                query = query.Where(booking =>
                    (booking.CompletedAt ?? booking.ScheduledStartTime) <
                        dateEnd);
            }
        }

        Sessions = await query
            .OrderByDescending(booking =>
                booking.CompletedAt ?? booking.ScheduledStartTime)
            .ThenByDescending(booking => booking.BookingId)
            .Select(booking => new SessionReviewListItem(
                booking.BookingId,
                booking.TutorCourseModule.Tutor.BcUser.DisplayName,
                booking.TutorCourseModule.Tutor.BcUser.PersonnelNumber,
                booking.StudentName,
                booking.SessionReviews.Any(review =>
                    review.Reviewer.Role == BcUserRole.HeadOfTutors),
                booking.ScheduledStartTime,
                booking.Duration))
            .ToListAsync(cancellationToken);
    }

    private static DateTimeOffset StartOfDay(DateOnly date) => new(
        date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
        SouthAfricaOffset);

    public sealed record SessionReviewListItem(
        int BookingId,
        string TutorName,
        string TutorPersonnelNumber,
        string StudentName,
        bool HasTutorHeadReview,
        DateTimeOffset ScheduledStartTime,
        SessionDuration Duration)
    {
        public string TutorDisplayName => string.IsNullOrWhiteSpace(TutorName)
            ? TutorPersonnelNumber
            : TutorName;

        public DateTimeOffset SessionEnd =>
            ScheduledStartTime.AddHours((int)Duration);

        public string ReviewStatus => HasTutorHeadReview
            ? "Reviewed"
            : "Awaiting review";
    }


    private sealed record ReviewPeriodDates(
        DateOnly StartDate,
        DateOnly EndDate,
        DateOnly Deadline)
    {
        public static ReviewPeriodDates ForMonth(DateOnly date)
        {
            var start = new DateOnly(date.Year, date.Month, 1);
            return new ReviewPeriodDates(
                start,
                start.AddMonths(1).AddDays(-1),
                start.AddMonths(1).AddDays(4));
        }
    }

    public sealed record ReviewPeriodSummary(
        DateOnly StartDate,
        DateOnly EndDate,
        DateOnly Deadline,
        int TotalSessions,
        int AwaitingReview,
        int Reviewed,
        int DaysRemaining)
    {
        public static ReviewPeriodSummary Empty { get; } = new(
            default, default, default, 0, 0, 0, 0);

        public string DeadlineStatus => DaysRemaining switch
        {
            > 1 => $"{DaysRemaining} days remaining",
            1 => "1 day remaining",
            0 => "Due today",
            -1 => "1 day overdue",
            _ => $"{Math.Abs(DaysRemaining)} days overdue"
        };
    }
}
