using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Administrator.Admin;

public class DashboardModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService? _currentUserService;
    private readonly TimeProvider _timeProvider;

    public DashboardModel(
        ApplicationDbContext context,
        ICurrentUserService? currentUserService = null,
        TimeProvider? timeProvider = null)
    {
        _context = context;
        _currentUserService = currentUserService;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public string AdminFirstName { get; private set; } = "Administrator";
    public int ActiveTutorCount { get; private set; }
    public int TutorResourceCount { get; private set; }
    public int PendingAdminRequestCount { get; private set; }
    public int CompletedSessionCount { get; private set; }
    public SessionActivityComparison WeeklyActivity { get; private set; } =
        SessionActivityComparison.Empty;
    public SessionActivityComparison MonthlyActivity { get; private set; } =
        SessionActivityComparison.Empty;
    public IReadOnlyList<TutorPerformanceItem> TutorPerformance
    { get; private set; } = [];
    public IReadOnlyList<BookedModuleItem> MostBookedModules
    { get; private set; } = [];
    public IReadOnlyList<DashboardEventItem> CurrentEvents
    { get; private set; } = [];

    public sealed record SessionActivityBucket(
        string Label,
        int PreviousCount,
        int CurrentCount,
        double AverageCount);

    public sealed record SessionActivityComparison(
        string PreviousLabel,
        string CurrentLabel,
        string AverageLabel,
        int PreviousTotal,
        int CurrentTotal,
        IReadOnlyList<SessionActivityBucket> Buckets)
    {
        public static SessionActivityComparison Empty { get; } =
            new(string.Empty, string.Empty, string.Empty, 0, 0, []);

        public int MaximumBucketCount => Math.Max(
            1,
            Buckets.Count == 0
                ? 0
                : Buckets.Max(bucket => Math.Max(
                    Math.Max(bucket.PreviousCount, bucket.CurrentCount),
                    (int)Math.Ceiling(bucket.AverageCount))));
    }

    public sealed record TutorPerformanceItem(
        int TutorId,
        string DisplayName,
        string Initials,
        string? ProfileImagePath,
        int CompletedSessions,
        double? AverageRating);

    public sealed record BookedModuleItem(
        string Code,
        string Name,
        int CompletedSessions);

    public sealed record DashboardEventItem(
        int CampusEventId,
        string Title,
        DateTimeOffset StartsAt,
        string BannerImagePath);

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        if (_currentUserService?.IsAuthenticated == true)
        {
            AdminFirstName = GetFirstName(
                _currentUserService.GetRequiredUser().DisplayName);
        }

        ActiveTutorCount = await _context.Tutors
            .AsNoTracking()
            .CountAsync(
                tutor =>
                    tutor.Status == TutorStatus.Approved &&
                    tutor.IsActive,
                cancellationToken);

        TutorResourceCount = await _context.LearningResources
            .AsNoTracking()
            .CountAsync(cancellationToken);

        int pendingTutorApplications = await _context.Tutors
            .AsNoTracking()
            .CountAsync(
                tutor => tutor.Status == TutorStatus.Pending,
                cancellationToken);

        int pendingModuleRequests = await _context.TutorModuleChangeRequests
            .AsNoTracking()
            .CountAsync(
                request => request.Status == TutorAccountRequestStatus.Pending,
                cancellationToken);

        PendingAdminRequestCount = pendingTutorApplications +
            pendingModuleRequests;

        CompletedSessionCount = await _context.Bookings
            .AsNoTracking()
            .CountAsync(
                booking => booking.Status == BookingStatus.Completed,
                cancellationToken);

        await LoadSessionActivityAsync(cancellationToken);
        await LoadTutorPerformanceAsync(cancellationToken);
        await LoadMostBookedModulesAsync(cancellationToken);
        await LoadCurrentEventsAsync(cancellationToken);
    }

    private async Task LoadCurrentEventsAsync(
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        CurrentEvents = await _context.CampusEvents
            .AsNoTracking()
            .Where(item => item.IsPublished &&
                item.PublishAt <= now &&
                item.EndsAt >= now)
            .OrderBy(item => item.StartsAt)
            .Select(item => new DashboardEventItem(
                item.CampusEventId,
                item.Title,
                item.StartsAt,
                item.BannerImagePath))
            .ToListAsync(cancellationToken);
    }

    private async Task LoadSessionActivityAsync(
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow()
            .ToOffset(TimeSpan.FromHours(2));
        DateTimeOffset today = new(
            now.Year,
            now.Month,
            now.Day,
            0,
            0,
            0,
            now.Offset);
        int daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
        DateTimeOffset currentWeekStart = today.AddDays(-daysSinceMonday);
        DateTimeOffset previousWeekStart = currentWeekStart.AddDays(-7);
        DateTimeOffset currentMonthStart = new(
            now.Year,
            now.Month,
            1,
            0,
            0,
            0,
            now.Offset);
        DateTimeOffset previousMonthStart = currentMonthStart.AddMonths(-1);
        DateTimeOffset activityHistoryStart = Min(
            currentWeekStart.AddDays(-28),
            currentMonthStart.AddMonths(-3));
        DateTimeOffset currentMonthEnd = currentMonthStart.AddMonths(1);
        DateTimeOffset activityHistoryEnd = Max(
            currentWeekStart.AddDays(7),
            currentMonthEnd);

        List<DateTimeOffset> completedDates = await _context.Bookings
            .AsNoTracking()
            .Where(booking =>
                booking.Status == BookingStatus.Completed &&
                (booking.CompletedAt ?? booking.ScheduledStartTime) >=
                    activityHistoryStart &&
                (booking.CompletedAt ?? booking.ScheduledStartTime) <
                    activityHistoryEnd)
            .Select(booking =>
                booking.CompletedAt ?? booking.ScheduledStartTime)
            .ToListAsync(cancellationToken);

        string[] dayLabels = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
        List<SessionActivityBucket> weeklyBuckets = [];
        for (int index = 0; index < 7; index++)
        {
            DateTimeOffset previousStart = previousWeekStart.AddDays(index);
            DateTimeOffset currentStart = currentWeekStart.AddDays(index);
            double fourWeekAverage = Enumerable.Range(1, 4)
                .Average(weekOffset =>
                {
                    DateTimeOffset start = currentWeekStart
                        .AddDays(index - weekOffset * 7);
                    return CountBetween(completedDates, start, start.AddDays(1));
                });
            weeklyBuckets.Add(new SessionActivityBucket(
                dayLabels[index],
                CountBetween(completedDates, previousStart, previousStart.AddDays(1)),
                CountBetween(completedDates, currentStart, currentStart.AddDays(1)),
                fourWeekAverage));
        }

        WeeklyActivity = new SessionActivityComparison(
            "Last week",
            "This week",
            "Previous 4-week average",
            weeklyBuckets.Sum(bucket => bucket.PreviousCount),
            weeklyBuckets.Sum(bucket => bucket.CurrentCount),
            weeklyBuckets);

        List<SessionActivityBucket> monthlyBuckets = [];
        for (int index = 0; index < 5; index++)
        {
            DateTimeOffset previousStart = previousMonthStart.AddDays(index * 7);
            DateTimeOffset previousEnd = Min(
                previousStart.AddDays(7),
                currentMonthStart);
            DateTimeOffset currentStart = currentMonthStart.AddDays(index * 7);
            DateTimeOffset currentEnd = Min(
                currentStart.AddDays(7),
                currentMonthEnd);
            double threeMonthAverage = Enumerable.Range(1, 3)
                .Average(monthOffset =>
                {
                    DateTimeOffset monthStart = currentMonthStart
                        .AddMonths(-monthOffset);
                    DateTimeOffset monthEnd = monthStart.AddMonths(1);
                    DateTimeOffset start = monthStart.AddDays(index * 7);
                    DateTimeOffset end = Min(start.AddDays(7), monthEnd);
                    return start < monthEnd
                        ? CountBetween(completedDates, start, end)
                        : 0;
                });

            monthlyBuckets.Add(new SessionActivityBucket(
                index == 4 ? "29–end" : $"{index * 7 + 1}–{index * 7 + 7}",
                previousStart < currentMonthStart
                    ? CountBetween(completedDates, previousStart, previousEnd)
                    : 0,
                currentStart < currentMonthEnd
                    ? CountBetween(completedDates, currentStart, currentEnd)
                    : 0,
                threeMonthAverage));
        }

        MonthlyActivity = new SessionActivityComparison(
            $"Last month · {previousMonthStart:MMMM}",
            $"This month · {currentMonthStart:MMMM}",
            "Previous 3-month average",
            monthlyBuckets.Sum(bucket => bucket.PreviousCount),
            monthlyBuckets.Sum(bucket => bucket.CurrentCount),
            monthlyBuckets);
    }

    private async Task LoadTutorPerformanceAsync(
        CancellationToken cancellationToken)
    {
        var tutors = await _context.Tutors
            .AsNoTracking()
            .Where(tutor =>
                tutor.Status == TutorStatus.Approved &&
                tutor.IsActive)
            .Select(tutor => new
            {
                tutor.TutorId,
                DisplayName = tutor.BcUser == null
                    ? $"Tutor #{tutor.TutorId}"
                    : tutor.BcUser.DisplayName,
                tutor.ProfileImagePath
            })
            .ToListAsync(cancellationToken);

        Dictionary<int, int> completedCounts = await _context.Bookings
            .AsNoTracking()
            .Where(booking => booking.Status == BookingStatus.Completed)
            .GroupBy(booking => booking.TutorId)
            .Select(group => new
            {
                TutorId = group.Key,
                Count = group.Count()
            })
            .ToDictionaryAsync(
                item => item.TutorId,
                item => item.Count,
                cancellationToken);

        Dictionary<int, double> averageRatings = await _context.Bookings
            .AsNoTracking()
            .Where(booking =>
                booking.Status == BookingStatus.Completed &&
                booking.StudentEvaluation != null)
            .GroupBy(booking => booking.TutorId)
            .Select(group => new
            {
                TutorId = group.Key,
                Rating = group.Average(booking =>
                    (double)booking.StudentEvaluation!.ModeRating)
            })
            .ToDictionaryAsync(
                item => item.TutorId,
                item => item.Rating,
                cancellationToken);

        TutorPerformance = tutors
            .Select(tutor =>
            {
                string displayName = string.IsNullOrWhiteSpace(tutor.DisplayName)
                    ? $"Tutor #{tutor.TutorId}"
                    : tutor.DisplayName.Trim();
                return new TutorPerformanceItem(
                    tutor.TutorId,
                    displayName,
                    GetInitials(displayName),
                    tutor.ProfileImagePath,
                    completedCounts.GetValueOrDefault(tutor.TutorId),
                    averageRatings.TryGetValue(tutor.TutorId, out double rating)
                        ? rating
                        : null);
            })
            .OrderByDescending(tutor => tutor.CompletedSessions)
            .ThenByDescending(tutor => tutor.AverageRating)
            .ThenBy(tutor => tutor.DisplayName)
            .Take(5)
            .ToList();
    }

    private async Task LoadMostBookedModulesAsync(
        CancellationToken cancellationToken)
    {
        var mostBookedRows = await _context.Bookings
            .AsNoTracking()
            .Where(booking =>
                booking.Status == BookingStatus.Completed &&
                booking.ProgrammeModule != null)
            .GroupBy(booking => new
            {
                booking.ProgrammeModule.ModuleCode,
                booking.ProgrammeModule.ModuleName
            })
            .Select(group => new
            {
                Code = group.Key.ModuleCode,
                Name = group.Key.ModuleName,
                CompletedSessions = group.Count()
            })
            .OrderByDescending(module => module.CompletedSessions)
            .ThenBy(module => module.Code)
            .Take(5)
            .ToListAsync(cancellationToken);

        MostBookedModules = mostBookedRows
            .Select(module => new BookedModuleItem(
                module.Code,
                module.Name,
                module.CompletedSessions))
            .ToList();
    }

    private static int CountBetween(
        IEnumerable<DateTimeOffset> dates,
        DateTimeOffset start,
        DateTimeOffset end) => dates.Count(date => date >= start && date < end);

    private static DateTimeOffset Min(
        DateTimeOffset first,
        DateTimeOffset second) => first <= second ? first : second;

    private static DateTimeOffset Max(
        DateTimeOffset first,
        DateTimeOffset second) => first >= second ? first : second;

    private static string GetInitials(string displayName)
    {
        string[] parts = displayName.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length switch
        {
            0 => "T",
            1 => parts[0][..1].ToUpperInvariant(),
            _ => $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant()
        };
    }

    private static string GetFirstName(string displayName)
    {
        string[] parts = displayName.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? "Administrator" : parts[0];
    }
}
