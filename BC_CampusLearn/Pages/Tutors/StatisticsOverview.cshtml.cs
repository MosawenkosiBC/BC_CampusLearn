using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Tutors;

public class StatisticsOverviewModel : PageModel
{
    private static readonly HashSet<string> SupportedRanges =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "daily",
            "weekly",
            "monthly",
            "custom"
        };

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public StatisticsOverviewModel(
        ApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    [BindProperty(SupportsGet = true)]
    public string Range { get; set; } = "monthly";

    [BindProperty(SupportsGet = true)]
    public DateOnly? StartDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? EndDate { get; set; }

    public string DisplayName { get; private set; } = string.Empty;

    public string Initials { get; private set; } = string.Empty;

    public string StudentNumber { get; private set; } = string.Empty;

    public string? ProfileImagePath { get; private set; }

    public string PeriodLabel { get; private set; } = string.Empty;

    public string? DateRangeError { get; private set; }

    public TutorStatisticsViewModel Statistics { get; private set; } = new();

    public IReadOnlyList<PendingReviewSession> PendingStudentReviewSessions
    { get; private set; } = Array.Empty<PendingReviewSession>();

    public IReadOnlyList<PendingReviewSession> PendingTutorReviewSessions
    { get; private set; } = Array.Empty<PendingReviewSession>();

    public async Task<IActionResult> OnGetAsync(
        CancellationToken cancellationToken)
    {
        if (!SupportedRanges.Contains(Range))
        {
            Range = "monthly";
        }

        DateOnly today = DateOnly.FromDateTime(DateTime.Now);
        (DateOnly periodStart, DateOnly periodEnd) = ResolveDateRange(today);

        CurrentUser currentUser =
            _currentUserService.GetRequiredUser();

        var tutor = await _context.Tutors
            .AsNoTracking()
            .Where(item =>
                item.BcUserId == currentUser.BcUserId &&
                item.Status == TutorStatus.Approved &&
                item.IsActive)
            .Select(item => new
            {
                item.TutorId,
                item.BcUser.DisplayName,
                item.BcUser.PersonnelNumber,
                item.ProfileImagePath
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (tutor is null)
        {
            return Forbid();
        }

        SetIdentity(
            tutor.DisplayName,
            tutor.PersonnelNumber,
            tutor.ProfileImagePath,
            currentUser.DisplayName);

        List<StatisticsBookingRow> allBookings =
            await _context.Bookings
                .AsNoTracking()
                .Where(booking => booking.TutorId == tutor.TutorId)
                .Select(booking => new StatisticsBookingRow
                {
                    BookingId = booking.BookingId,
                    StudentName = booking.StudentName,
                    StudentBcUserId = booking.StudentBcUserId,
                    ModuleCode = booking.ProgrammeModule.ModuleCode,
                    Status = booking.Status,
                    HasStudentReview = booking.StudentEvaluation != null,
                    StudentReviewRating = booking.StudentEvaluation == null
                        ? null
                        : booking.StudentEvaluation.ModeRating,
                    HasTutorReview = booking.TutorEvaluation != null,
                    CompletedAt = booking.CompletedAt,
                    ScheduledStartTime = booking.ScheduledStartTime
                })
                .ToListAsync(cancellationToken);

        List<StatisticsBookingRow> periodBookings = allBookings
            .Where(booking =>
            {
                DateOnly bookingDate = DateOnly.FromDateTime(
                    booking.ScheduledStartTime.LocalDateTime);
                return bookingDate >= periodStart && bookingDate <= periodEnd;
            })
            .ToList();

        Statistics = BuildStatistics(periodBookings);

        List<StatisticsBookingRow> completedBookings = periodBookings
            .Where(booking => booking.Status == BookingStatus.Completed)
            .ToList();
        PendingStudentReviewSessions = BuildPendingReviewSessions(
            completedBookings.Where(booking => !booking.HasStudentReview));
        PendingTutorReviewSessions = BuildPendingReviewSessions(
            completedBookings.Where(booking => !booking.HasTutorReview));

        return Page();
    }

    private static TutorStatisticsViewModel BuildStatistics(
        IReadOnlyCollection<StatisticsBookingRow> periodBookings)
    {
        List<StatisticsBookingRow> completed = periodBookings
            .Where(booking => booking.Status == BookingStatus.Completed)
            .ToList();
        int cancelled = periodBookings.Count(booking =>
            booking.Status == BookingStatus.Cancelled);
        int concludedAcceptedSessions = completed.Count + cancelled;
        List<byte> studentReviewRatings = completed
            .Where(booking => booking.StudentReviewRating.HasValue)
            .Select(booking => booking.StudentReviewRating!.Value)
            .ToList();

        TutorStatisticsViewModel statistics = new()
        {
            CompletedSessions = completed.Count,
            UniqueStudents = completed
                .Where(booking => booking.StudentBcUserId.HasValue)
                .Select(booking => booking.StudentBcUserId)
                .Distinct()
                .Count(),
            TutoringHours = completed.Count,
            CompletionRate = concludedAcceptedSessions == 0
                ? 0
                : Math.Round(
                    completed.Count * 100m / concludedAcceptedSessions,
                    1),
            PendingRequests = periodBookings.Count(booking =>
                booking.Status == BookingStatus.Pending),
            PendingStudentReviews = completed.Count(booking =>
                !booking.HasStudentReview),
            PendingTutorReviews = completed.Count(booking =>
                !booking.HasTutorReview),
            StudentReviewCount = studentReviewRatings.Count,
            AverageStudentReviewRating = studentReviewRatings.Count < 2
                ? 0
                : Math.Round(studentReviewRatings.Average(value => (decimal)value), 1)
        };

        var moduleCounts = completed
            .GroupBy(booking => new
            {
                booking.ModuleCode
            })
            .Select(group => new
            {
                group.Key.ModuleCode,
                Count = group.Count()
            })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.ModuleCode)
            .Take(5)
            .ToList();

        statistics.TopModules = moduleCounts
            .Select(item => new TutorModuleStatisticViewModel
            {
                ModuleCode = item.ModuleCode,
                SessionCount = item.Count
            })
            .ToList();

        return statistics;
    }

    private static IReadOnlyList<PendingReviewSession> BuildPendingReviewSessions(
        IEnumerable<StatisticsBookingRow> bookings) => bookings
            .OrderByDescending(booking => booking.CompletedAt)
            .Select(booking => new PendingReviewSession(
                booking.BookingId,
                booking.StudentName,
                booking.ModuleCode,
                booking.CompletedAt))
            .ToList();

    private (DateOnly Start, DateOnly End) ResolveDateRange(DateOnly today)
    {
        switch (Range.ToLowerInvariant())
        {
            case "daily":
                PeriodLabel = "Today";
                return (today, today);
            case "weekly":
                PeriodLabel = "Last 7 days";
                return (today.AddDays(-6), today);
            case "custom" when StartDate.HasValue && EndDate.HasValue &&
                StartDate.Value <= EndDate.Value:
                PeriodLabel = StartDate.Value == EndDate.Value
                    ? StartDate.Value.ToString("dd MMM yyyy")
                    : $"{StartDate.Value:dd MMM yyyy} – {EndDate.Value:dd MMM yyyy}";
                return (StartDate.Value, EndDate.Value);
            case "custom":
                DateRangeError = StartDate.HasValue && EndDate.HasValue
                    ? "The start date must be on or before the end date."
                    : "Choose both a start date and an end date.";
                PeriodLabel = "Last 30 days";
                return (today.AddDays(-29), today);
            default:
                PeriodLabel = "Last 30 days";
                return (today.AddDays(-29), today);
        }
    }

    private void SetIdentity(
        string storedDisplayName,
        string personnelNumber,
        string? profileImagePath,
        string currentDisplayName)
    {
        DisplayName = !string.IsNullOrWhiteSpace(storedDisplayName)
            ? storedDisplayName
            : !string.IsNullOrWhiteSpace(currentDisplayName)
                ? currentDisplayName
                : "Tutor";
        StudentNumber = personnelNumber;
        ProfileImagePath = profileImagePath;

        string[] nameParts = DisplayName.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries);

        Initials = nameParts.Length switch
        {
            > 1 => $"{nameParts[0][0]}{nameParts[^1][0]}"
                .ToUpperInvariant(),
            1 => nameParts[0][..1].ToUpperInvariant(),
            _ => "T"
        };
    }

    private sealed class StatisticsBookingRow
    {
        public int BookingId { get; set; }

        public string StudentName { get; set; } = string.Empty;

        public int? StudentBcUserId { get; set; }

        public string ModuleCode { get; set; } = string.Empty;

        public BookingStatus Status { get; set; }

        public bool HasStudentReview { get; set; }

        public byte? StudentReviewRating { get; set; }

        public bool HasTutorReview { get; set; }

        public DateTimeOffset ScheduledStartTime { get; set; }

        public DateTimeOffset? CompletedAt { get; set; }

    }

    public sealed record PendingReviewSession(
        int BookingId,
        string StudentName,
        string ModuleCode,
        DateTimeOffset? CompletedAt);
}
