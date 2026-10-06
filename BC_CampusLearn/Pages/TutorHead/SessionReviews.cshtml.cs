using BC_CampusLearn.Data;
using BC_CampusLearn.Authentication;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Gemini;
using BC_CampusLearn.Services.Settings;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.TutorHead;

[Authorize(Roles = nameof(BcUserRole.HeadOfTutors))]
public class SessionReviewsModel(
    ApplicationDbContext context,
    TimeProvider? timeProvider = null,
    ICurrentUserService? currentUserService = null,
    IGeminiApiKeyProtector? apiKeyProtector = null) : PageModel
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

    [BindProperty(SupportsGet = true)]
    public string? ReviewStatusFilter { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Enter a Gemini API key.")]
    [StringLength(512, MinimumLength = 10,
        ErrorMessage = "Enter a valid Gemini API key.")]
    [Display(Name = "Gemini API key")]
    public string? GeminiApiKey { get; set; }

    public bool HasGeminiApiKey { get; private set; }

    public bool OpenGeminiModal { get; private set; }

    [TempData]
    public string? GeminiKeyMessage { get; set; }

    public string? DateRangeError { get; private set; }

    public IReadOnlyList<SessionReviewListItem> Sessions { get; private set; }
        = [];

    public ReviewPeriodSummary PeriodSummary { get; private set; } =
        ReviewPeriodSummary.Empty;

    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(TutorFilter) ||
        !string.IsNullOrWhiteSpace(StudentFilter) ||
        DateFrom.HasValue ||
        DateTo.HasValue ||
        ReviewStatusFilter is not null;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset currentUtc = (timeProvider ?? TimeProvider.System)
            .GetUtcNow();
        if (currentUserService is not null)
        {
            int userId = currentUserService.GetRequiredUser().BcUserId;
            BcUser? user = await context.BcUsers.SingleOrDefaultAsync(
                item => item.BcUserId == userId,
                cancellationToken);
            if (user is not null)
            {
                HasGeminiApiKey = user.EncryptedGeminiApiKey is not null;
                user.SessionReviewsLastViewedAt = currentUtc;
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        TutorFilter = string.IsNullOrWhiteSpace(TutorFilter)
            ? null
            : TutorFilter.Trim();
        StudentFilter = string.IsNullOrWhiteSpace(StudentFilter)
            ? null
            : StudentFilter.Trim();
        ReviewStatusFilter = ReviewStatusFilter?.Trim().ToLowerInvariant();
        if (ReviewStatusFilter is not ("awaiting" or "reviewed"))
        {
            ReviewStatusFilter = null;
        }

        DateTimeOffset now = currentUtc.ToOffset(SouthAfricaOffset);
        DateOnly today = DateOnly.FromDateTime(now.DateTime);
        ReviewDeadlineSettings? configuredDeadline =
            await context.PlatformSettings
            .AsNoTracking()
            .Where(settings => settings.PlatformSettingsId ==
                PlatformSettings.SingletonId)
            .Select(settings => new ReviewDeadlineSettings(
                settings.TutorHeadReviewDeadline,
                settings.IsTutorHeadReviewDeadlineRecurring,
                settings.UseLastDayOfMonthForTutorHeadReviewDeadline))
            .SingleOrDefaultAsync(cancellationToken);
        ReviewPeriodWindow period = configuredDeadline is not null
            ? MonthlyReviewPeriod.Resolve(
                configuredDeadline.Deadline,
                configuredDeadline.UseLastDayOfMonth,
                today)
            : ForCurrentMonth(today);
        DateOnly displayedDeadline = configuredDeadline is
            { IsRecurring: false }
                ? configuredDeadline.Deadline
                : period.Deadline;
        DateTimeOffset periodStart = StartOfDay(period.StartDate);
        DateTimeOffset periodEndExclusive =
            StartOfDay(period.EndDate.AddDays(1));

        IQueryable<Booking> eligibleSessions = context.Bookings
            .AsNoTracking()
            .Where(booking =>
                booking.Status == BookingStatus.Completed &&
                booking.StudentEvaluation != null &&
                booking.TutorEvaluation != null);

        IQueryable<Booking> activeReviewSessions = eligibleSessions.Where(
            booking =>
                (booking.CompletedAt ?? booking.ScheduledStartTime) <
                    periodEndExclusive &&
                ((booking.CompletedAt ?? booking.ScheduledStartTime) >=
                    periodStart ||
                 !booking.SessionReviews.Any(review =>
                    review.Reviewer.Role == BcUserRole.HeadOfTutors)));

        int totalSessions = await activeReviewSessions.CountAsync(
            cancellationToken);
        int reviewedSessions = await activeReviewSessions.CountAsync(
            booking => booking.SessionReviews.Any(review =>
                review.Reviewer.Role == BcUserRole.HeadOfTutors),
            cancellationToken);
        PeriodSummary = new ReviewPeriodSummary(
            period.StartDate,
            period.EndDate,
            displayedDeadline,
            totalSessions,
            totalSessions - reviewedSessions,
            reviewedSessions,
            displayedDeadline.DayNumber - today.DayNumber);

        bool hasCustomDateRange = DateFrom.HasValue || DateTo.HasValue;
        IQueryable<Booking> query = hasCustomDateRange
            ? eligibleSessions
            : activeReviewSessions;

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

        if (ReviewStatusFilter == "reviewed")
        {
            query = query.Where(booking => booking.SessionReviews.Any(review =>
                review.Reviewer.Role == BcUserRole.HeadOfTutors));
        }
        else if (ReviewStatusFilter == "awaiting")
        {
            query = query.Where(booking => !booking.SessionReviews.Any(review =>
                review.Reviewer.Role == BcUserRole.HeadOfTutors));
        }

        Sessions = await query
            .OrderBy(booking => booking.SessionReviews.Any(review =>
                review.Reviewer.Role == BcUserRole.HeadOfTutors))
            .ThenByDescending(booking =>
                booking.CompletedAt ?? booking.ScheduledStartTime)
            .ThenByDescending(booking => booking.BookingId)
            .Select(booking => new SessionReviewListItem(
                booking.BookingId,
                booking.TutorCourseModule.Tutor.BcUser.DisplayName,
                booking.TutorCourseModule.Tutor.BcUser.PersonnelNumber,
                booking.StudentName,
                booking.ProgrammeModule.ModuleCode,
                booking.SessionReviews.Any(review =>
                    review.Reviewer.Role == BcUserRole.HeadOfTutors),
                booking.ScheduledStartTime,
                booking.Duration))
            .ToListAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostSaveGeminiKeyAsync(
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            OpenGeminiModal = true;
            await OnGetAsync(cancellationToken);
            return Page();
        }

        if (currentUserService is null || apiKeyProtector is null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        int userId = currentUserService.GetRequiredUser().BcUserId;
        BcUser? user = await context.BcUsers.SingleOrDefaultAsync(
            item => item.BcUserId == userId,
            cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        user.EncryptedGeminiApiKey = apiKeyProtector.Protect(GeminiApiKey!);
        await context.SaveChangesAsync(cancellationToken);
        GeminiKeyMessage = "Gemini API key saved securely.";
        return RedirectToPage();
    }

    private static DateTimeOffset StartOfDay(DateOnly date) => new(
        date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
        SouthAfricaOffset);

    public sealed record SessionReviewListItem(
        int BookingId,
        string TutorName,
        string TutorPersonnelNumber,
        string StudentName,
        string ModuleCode,
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


    private static ReviewPeriodWindow ForCurrentMonth(DateOnly date)
    {
        DateOnly start = new(date.Year, date.Month, 1);
        return new ReviewPeriodWindow(
            start,
            start.AddMonths(1).AddDays(-1),
            start.AddMonths(1).AddDays(4));
    }

    private sealed record ReviewDeadlineSettings(
        DateOnly Deadline,
        bool IsRecurring,
        bool UseLastDayOfMonth);

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
            _ => string.Empty
        };
    }
}
