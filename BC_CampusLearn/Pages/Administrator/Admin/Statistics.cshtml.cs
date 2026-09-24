using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Administrator.Admin;

public class StatisticsModel(ApplicationDbContext context, TimeProvider timeProvider) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string BookingPeriod { get; set; } = "all";

    [BindProperty(SupportsGet = true)]
    public DateOnly? BookingStartDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? BookingEndDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool ShowBookedModules { get; set; }

    [BindProperty(SupportsGet = true)]
    public string ReviewPeriod { get; set; } = "all";

    [BindProperty(SupportsGet = true)]
    public DateOnly? ReviewStartDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? ReviewEndDate { get; set; }

    public string BookingPeriodLabel { get; private set; } = "All time";
    public int TotalTutorCount { get; private set; }
    public int ActiveTutorCount { get; private set; }
    public int PendingApplicationCount { get; private set; }
    public int TotalModuleCount { get; private set; }
    public int CoveredModuleCount { get; private set; }
    public int TotalSessionCount { get; private set; }
    public int OutcomeSessionCount { get; private set; }
    public int CompletedSessionCount { get; private set; }
    public int CancelledSessionCount { get; private set; }
    public int DeclinedSessionCount { get; private set; }
    public int TotalStudentCount { get; private set; }
    public int PendingStudentReviewCount { get; private set; }
    public int PendingTutorReviewCount { get; private set; }
    public int PendingTutorHeadReviewCount { get; private set; }
    public int AwaitingAdminReviewCount { get; private set; }
    public int BothReviewsCompleteCount { get; private set; }
    public int AdminReviewsRecordedCount { get; private set; }
    public int UniqueBookingStudentCount { get; private set; }
    public double? AverageTutorRating { get; private set; }
    public double? AveragePlatformRating { get; private set; }
    public double CompletionRate { get; private set; }
    public IReadOnlyList<AssignedModuleStatistics> AssignedModules { get; private set; } = [];
    public IReadOnlyList<AssignedModuleStatistics> TopAssignedModules { get; private set; } = [];
    public IReadOnlyList<CampusTutorStatistics> TutorsByCampus { get; private set; } = [];
    public int MaxCampusTutorCount { get; private set; }
    public IReadOnlyList<BookedModuleStatistics> BookedModules { get; private set; } = [];
    public IReadOnlyList<BookedModuleStatistics> TopBookedModules { get; private set; } = [];
    public int TotalBookedSessions { get; private set; }
    public int FilteredBookedSessionCount { get; private set; }
    public string TopPieGradient { get; private set; } = "none";
    public string AllPieGradient { get; private set; } = "none";
    public IReadOnlyList<CompletedTutorStatistics> TopCompletedTutors { get; private set; } = [];
    public string TopCompletedTutorPieGradient { get; private set; } = "none";
    public IReadOnlyList<SessionStatusStatistics> SessionStatuses { get; private set; } = [];

    public sealed record AssignedModuleStatistics(string Code, string Name, int ActiveTutors);
    public sealed record CampusTutorStatistics(string Campus, int TutorCount, string Color);
    public sealed record BookedModuleStatistics(
        string Code, string Name, int Count, double Percentage, string Color);
    public sealed record CompletedTutorStatistics(
        string Name, int Count, double Percentage, string Color);

    public sealed record SessionStatusStatistics(BookingStatus Status, int Count, string Color);

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        BookingPeriod = BookingPeriod is "all" or "monthly" or "weekly" or "daily" or "custom"
            ? BookingPeriod
            : "all";
        if (BookingStartDate.HasValue && BookingEndDate.HasValue &&
            BookingStartDate > BookingEndDate)
        {
            (BookingStartDate, BookingEndDate) = (BookingEndDate, BookingStartDate);
        }
        ReviewPeriod = ReviewPeriod is "all" or "monthly" or "weekly" or "daily" or "custom"
            ? ReviewPeriod
            : "all";
        if (ReviewStartDate.HasValue && ReviewEndDate.HasValue &&
            ReviewStartDate > ReviewEndDate)
        {
            (ReviewStartDate, ReviewEndDate) = (ReviewEndDate, ReviewStartDate);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        IQueryable<Booking> overallBookings = context.Bookings.AsNoTracking()
            .Where(booking => booking.ScheduledStartTime <= now);

        IQueryable<Tutor> activeTutors = context.Tutors.AsNoTracking()
            .Where(tutor => tutor.Status == TutorStatus.Approved &&
                tutor.IsActive);

        TotalTutorCount = await context.Tutors.AsNoTracking().CountAsync(cancellationToken);
        ActiveTutorCount = await activeTutors.CountAsync(cancellationToken);
        List<string> activeTutorCampuses = await activeTutors
            .Select(tutor => tutor.CampusOfStudy)
            .ToListAsync(cancellationToken);
        TutorsByCampus = activeTutorCampuses
            .GroupBy(campus => string.IsNullOrWhiteSpace(campus) ? "Not specified" : campus.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .Select(group => new { Campus = group.Key, TutorCount = group.Count() })
            .OrderByDescending(campus => campus.TutorCount)
            .ThenBy(campus => campus.Campus)
            .Select((campus, index) => new CampusTutorStatistics(
                campus.Campus, campus.TutorCount, PieColor(index)))
            .ToList();
        MaxCampusTutorCount = TutorsByCampus.Count == 0
            ? 0
            : TutorsByCampus.Max(campus => campus.TutorCount);
        TotalStudentCount = await context.BcUsers.AsNoTracking()
            .CountAsync(user => user.Role == BcUserRole.Student, cancellationToken);
        PendingApplicationCount = await context.Tutors.AsNoTracking()
            .CountAsync(tutor => tutor.Status == TutorStatus.Pending, cancellationToken);
        var moduleCatalog = await context.ProgrammeModules.AsNoTracking()
            .Select(module => new
            {
                Id = module.ProgrammeModuleId,
                Code = module.ModuleCode,
                Name = module.ModuleName
            })
            .ToListAsync(cancellationToken);
        var tutorCounts = await context.TutorCourseModules.AsNoTracking().Where(a => a.IsActive)
            .Where(assignment => assignment.Tutor.Status == TutorStatus.Approved &&
                assignment.Tutor.IsActive)
            .GroupBy(assignment => assignment.ProgrammeModuleId)
            .Select(group => new { ModuleId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.ModuleId, item => item.Count, cancellationToken);
        TotalModuleCount = moduleCatalog.Count;
        AssignedModules = moduleCatalog
            .Where(module => tutorCounts.ContainsKey(module.Id))
            .Select(module => new AssignedModuleStatistics(
                module.Code, module.Name, tutorCounts[module.Id]))
            .OrderByDescending(module => module.ActiveTutors)
            .ThenBy(module => module.Name)
            .ToList();
        TopAssignedModules = AssignedModules.Take(5).ToList();
        CoveredModuleCount = AssignedModules.Count;

        IQueryable<Booking> completed = overallBookings
            .Where(booking => booking.Status == BookingStatus.Completed);
        IQueryable<Booking> reviewCompleted = completed;
        switch (ReviewPeriod)
        {
            case "daily":
                reviewCompleted = reviewCompleted
                    .Where(booking => booking.ScheduledStartTime >= now.AddDays(-1));
                break;
            case "weekly":
                reviewCompleted = reviewCompleted
                    .Where(booking => booking.ScheduledStartTime >= now.AddDays(-7));
                break;
            case "monthly":
                reviewCompleted = reviewCompleted
                    .Where(booking => booking.ScheduledStartTime >= now.AddDays(-30));
                break;
            case "custom":
                if (ReviewStartDate.HasValue)
                {
                    DateTimeOffset start = new(ReviewStartDate.Value.ToDateTime(TimeOnly.MinValue),
                        TimeSpan.Zero);
                    reviewCompleted = reviewCompleted
                        .Where(booking => booking.ScheduledStartTime >= start);
                }
                if (ReviewEndDate.HasValue)
                {
                    DateTimeOffset endExclusive = new(
                        ReviewEndDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
                    reviewCompleted = reviewCompleted
                        .Where(booking => booking.ScheduledStartTime < endExclusive);
                }
                break;
        }
        UniqueBookingStudentCount = await reviewCompleted
            .Where(booking => booking.StudentBcUserId.HasValue)
            .Select(booking => booking.StudentBcUserId!.Value)
            .Distinct()
            .CountAsync(cancellationToken);
        IQueryable<Booking> filteredCompleted = completed;
        switch (BookingPeriod)
        {
            case "daily":
                filteredCompleted = filteredCompleted
                    .Where(booking => booking.ScheduledStartTime >= now.AddDays(-1));
                BookingPeriodLabel = "Past 24 hours";
                break;
            case "weekly":
                filteredCompleted = filteredCompleted
                    .Where(booking => booking.ScheduledStartTime >= now.AddDays(-7));
                BookingPeriodLabel = "Past 7 days";
                break;
            case "monthly":
                filteredCompleted = filteredCompleted
                    .Where(booking => booking.ScheduledStartTime >= now.AddDays(-30));
                BookingPeriodLabel = "Past 30 days";
                break;
            case "custom":
                if (BookingStartDate.HasValue)
                {
                    DateTimeOffset start = new(BookingStartDate.Value.ToDateTime(TimeOnly.MinValue),
                        TimeSpan.Zero);
                    filteredCompleted = filteredCompleted
                        .Where(booking => booking.ScheduledStartTime >= start);
                }
                if (BookingEndDate.HasValue)
                {
                    DateTimeOffset endExclusive = new(
                        BookingEndDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
                    filteredCompleted = filteredCompleted
                        .Where(booking => booking.ScheduledStartTime < endExclusive);
                }
                BookingPeriodLabel = BookingStartDate.HasValue || BookingEndDate.HasValue
                    ? $"{BookingStartDate?.ToString("dd MMM yyyy") ?? "Beginning"} – " +
                      $"{BookingEndDate?.ToString("dd MMM yyyy") ?? "Today"}"
                    : "Custom dates";
                break;
        }

        var allBookingsByModule = await completed
            .GroupBy(booking => booking.ProgrammeModuleId)
            .Select(group => new { ModuleId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var moduleNames = moduleCatalog.ToDictionary(module => module.Id);
        var allRankedModules = allBookingsByModule
            .Where(item => moduleNames.ContainsKey(item.ModuleId))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => moduleNames[item.ModuleId].Code)
            .ToList();
        TotalBookedSessions = allRankedModules.Sum(item => item.Count);
        List<BookedModuleStatistics> allModuleStatistics = allRankedModules.Select((item, index) =>
        {
            var module = moduleNames[item.ModuleId];
            return new BookedModuleStatistics(module.Code, module.Name, item.Count,
                item.Count * 100d / TotalBookedSessions, PieColor(index));
        }).ToList();
        List<BookedModuleStatistics> topModules = allModuleStatistics.Take(5).ToList();
        if (allModuleStatistics.Count > 5)
        {
            int remainingCount = allModuleStatistics.Skip(5).Sum(item => item.Count);
            topModules.Add(new BookedModuleStatistics(string.Empty, "Other modules",
                remainingCount, remainingCount * 100d / TotalBookedSessions, "#cbd5dc"));
        }
        TopBookedModules = topModules;
        TopPieGradient = PieGradient(TopBookedModules);

        var filteredBookingsByModule = BookingPeriod == "all"
            ? allBookingsByModule
            : await filteredCompleted
                .GroupBy(booking => booking.ProgrammeModuleId)
                .Select(group => new { ModuleId = group.Key, Count = group.Count() })
                .ToListAsync(cancellationToken);
        var filteredRankedModules = filteredBookingsByModule
            .Where(item => moduleNames.ContainsKey(item.ModuleId))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => moduleNames[item.ModuleId].Code)
            .ToList();
        FilteredBookedSessionCount = filteredRankedModules.Sum(item => item.Count);
        BookedModules = filteredRankedModules.Select((item, index) =>
        {
            var module = moduleNames[item.ModuleId];
            return new BookedModuleStatistics(module.Code, module.Name, item.Count,
                item.Count * 100d / FilteredBookedSessionCount, PieColor(index));
        }).ToList();
        AllPieGradient = PieGradient(BookedModules);

        var topTutorCounts = await filteredCompleted
            .Where(booking => booking.TutorCourseModule.Tutor.Status == TutorStatus.Approved &&
                booking.TutorCourseModule.Tutor.IsActive)
            .GroupBy(booking => booking.TutorId)
            .Select(group => new { TutorId = group.Key, Count = group.Count() })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.TutorId)
            .Take(5)
            .ToListAsync(cancellationToken);
        int[] topTutorIds = topTutorCounts.Select(item => item.TutorId).ToArray();
        var tutorNames = await context.Tutors.AsNoTracking()
            .Where(tutor => topTutorIds.Contains(tutor.TutorId))
            .Select(tutor => new
            {
                tutor.TutorId,
                Name = tutor.BcUser == null ? string.Empty : tutor.BcUser.DisplayName
            })
            .ToDictionaryAsync(item => item.TutorId, item => item.Name, cancellationToken);
        int topTutorCompletedTotal = topTutorCounts.Sum(item => item.Count);
        TopCompletedTutors = topTutorCounts.Select((item, index) =>
        {
            string? name = tutorNames.GetValueOrDefault(item.TutorId);
            if (string.IsNullOrWhiteSpace(name)) name = $"Tutor #{item.TutorId}";
            return new CompletedTutorStatistics(name, item.Count,
                item.Count * 100d / topTutorCompletedTotal, PieColor(index));
        }).ToList();
        TopCompletedTutorPieGradient = PieGradient(TopCompletedTutors);

        var statuses = await overallBookings
            .GroupBy(booking => booking.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Status, item => item.Count, cancellationToken);
        SessionStatuses = Enum.GetValues<BookingStatus>()
            .Where(status => status != BookingStatus.Pending)
            .Select(status => new SessionStatusStatistics(status,
                statuses.GetValueOrDefault(status), SessionStatusColor(status)))
            .ToList();
        TotalSessionCount = statuses.Values.Sum();
        OutcomeSessionCount = SessionStatuses.Sum(status => status.Count);
        CompletedSessionCount = statuses.GetValueOrDefault(BookingStatus.Completed);
        CancelledSessionCount = statuses.GetValueOrDefault(BookingStatus.Cancelled);
        DeclinedSessionCount = statuses.GetValueOrDefault(BookingStatus.Declined);
        int resolved = CompletedSessionCount + CancelledSessionCount + DeclinedSessionCount;
        CompletionRate = resolved == 0 ? 0 : CompletedSessionCount * 100d / resolved;

        PendingStudentReviewCount = await reviewCompleted
            .CountAsync(booking => booking.StudentEvaluation == null, cancellationToken);
        PendingTutorReviewCount = await reviewCompleted
            .CountAsync(booking => booking.TutorEvaluation == null, cancellationToken);
        PendingTutorHeadReviewCount = await reviewCompleted
            .CountAsync(booking => booking.StudentEvaluation != null &&
                booking.TutorEvaluation != null &&
                booking.AdminSessionReview == null, cancellationToken);
        AwaitingAdminReviewCount = await reviewCompleted
            .CountAsync(booking => booking.StudentEvaluation != null &&
                booking.TutorEvaluation != null &&
                booking.AdminSessionReview != null, cancellationToken);
        BothReviewsCompleteCount = await reviewCompleted
            .CountAsync(booking => booking.StudentEvaluation != null &&
                booking.TutorEvaluation != null, cancellationToken);
        AdminReviewsRecordedCount = await reviewCompleted
            .CountAsync(booking => booking.AdminSessionReview != null, cancellationToken);
        AverageTutorRating = await reviewCompleted
            .Where(booking => booking.StudentEvaluation != null)
            .AverageAsync(booking => (double?)booking.StudentEvaluation!.ModeRating,
                cancellationToken);
        AveragePlatformRating = await reviewCompleted
            .Where(booking => booking.StudentEvaluation != null)
            .AverageAsync(booking => (double?)booking.StudentEvaluation!.PlatformRating,
                cancellationToken);

    }

    private static string PieColor(int index)
    {
        string[] palette = ["#a43b70", "#4f87a5", "#df944d", "#6c9b71", "#9273b1"];
        return index < palette.Length ? palette[index] :
            $"hsl({(index * 137.508 % 360).ToString("0.###", CultureInfo.InvariantCulture)} 52% 48%)";
    }

    private static string SessionStatusColor(BookingStatus status) => status switch
    {
        BookingStatus.Confirmed => "#7fbe8c",
        BookingStatus.Completed => "#75add0",
        BookingStatus.Cancelled or BookingStatus.Declined => "#d85c5c",
        BookingStatus.InProgress => "#9b78bd",
        _ => "#8b969d"
    };

    private static string PieGradient(IReadOnlyList<BookedModuleStatistics> modules)
        => PieGradient(modules.Select(module => (module.Percentage, module.Color)).ToList());

    private static string PieGradient(IReadOnlyList<CompletedTutorStatistics> tutors)
        => PieGradient(tutors.Select(tutor => (tutor.Percentage, tutor.Color)).ToList());

    private static string PieGradient(IReadOnlyList<(double Percentage, string Color)> slices)
    {
        if (slices.Count == 0) return "none";
        double end = 0;
        List<string> stops = [];
        for (int index = 0; index < slices.Count; index++)
        {
            double start = end;
            end = index == slices.Count - 1 ? 100 : end + slices[index].Percentage;
            stops.Add($"{slices[index].Color} " +
                $"{start.ToString("0.###", CultureInfo.InvariantCulture)}% " +
                $"{end.ToString("0.###", CultureInfo.InvariantCulture)}%");
        }
        return $"conic-gradient({string.Join(", ", stops)})";
    }
}
