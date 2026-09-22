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
    public string Period { get; set; } = "90";

    public string PeriodLabel { get; private set; } = string.Empty;
    public int TotalTutorCount { get; private set; }
    public int ActiveTutorCount { get; private set; }
    public int PendingApplicationCount { get; private set; }
    public int TotalModuleCount { get; private set; }
    public int CoveredModuleCount { get; private set; }
    public int TotalSessionCount { get; private set; }
    public int CompletedSessionCount { get; private set; }
    public int CancelledSessionCount { get; private set; }
    public int DeclinedSessionCount { get; private set; }
    public int TotalStudentCount { get; private set; }
    public int PendingStudentReviewCount { get; private set; }
    public int PendingTutorReviewCount { get; private set; }
    public int BothReviewsCompleteCount { get; private set; }
    public int AdminReviewsRecordedCount { get; private set; }
    public double? AverageStudentRating { get; private set; }
    public double CompletionRate { get; private set; }
    public IReadOnlyList<AssignedModuleStatistics> AssignedModules { get; private set; } = [];
    public IReadOnlyList<AssignedModuleStatistics> TopAssignedModules { get; private set; } = [];
    public IReadOnlyList<BookedModuleStatistics> BookedModules { get; private set; } = [];
    public IReadOnlyList<BookedModuleStatistics> TopBookedModules { get; private set; } = [];
    public int TotalBookedSessions { get; private set; }
    public string TopPieGradient { get; private set; } = "none";
    public string AllPieGradient { get; private set; } = "none";
    public IReadOnlyList<SessionStatusStatistics> SessionStatuses { get; private set; } = [];

    public sealed record AssignedModuleStatistics(string Code, string Name, int ActiveTutors);
    public sealed record BookedModuleStatistics(
        string Code, string Name, int Count, double Percentage, string Color);

    public sealed record SessionStatusStatistics(BookingStatus Status, int Count);

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Period = Period is "30" or "90" or "365" or "all" ? Period : "90";
        PeriodLabel = Period switch
        {
            "30" => "Past 30 days",
            "365" => "Past 12 months",
            "all" => "All time",
            _ => "Past 90 days"
        };

        DateTimeOffset now = timeProvider.GetUtcNow();
        IQueryable<Booking> periodBookings = context.Bookings.AsNoTracking()
            .Where(booking => booking.ScheduledStartTime <= now);
        if (Period != "all")
        {
            DateTimeOffset start = now.AddDays(int.Parse(Period, System.Globalization.CultureInfo.InvariantCulture) * -1);
            periodBookings = periodBookings.Where(booking => booking.ScheduledStartTime >= start);
        }

        IQueryable<Tutor> activeTutors = context.Tutors.AsNoTracking()
            .Where(tutor => tutor.Status == TutorStatus.Approved &&
                tutor.IsActive);

        TotalTutorCount = await context.Tutors.AsNoTracking().CountAsync(cancellationToken);
        ActiveTutorCount = await activeTutors.CountAsync(cancellationToken);
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

        var bookingsByModule = await periodBookings
            .GroupBy(booking => booking.ProgrammeModuleId)
            .Select(group => new { ModuleId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var moduleNames = moduleCatalog.ToDictionary(module => module.Id);
        var rankedModules = bookingsByModule
            .Where(item => moduleNames.ContainsKey(item.ModuleId))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => moduleNames[item.ModuleId].Code)
            .ToList();
        TotalBookedSessions = rankedModules.Sum(item => item.Count);
        BookedModules = rankedModules.Select((item, index) =>
        {
            var module = moduleNames[item.ModuleId];
            return new BookedModuleStatistics(module.Code, module.Name, item.Count,
                item.Count * 100d / TotalBookedSessions, PieColor(index));
        }).ToList();
        List<BookedModuleStatistics> topModules = BookedModules.Take(5).ToList();
        if (BookedModules.Count > 5)
        {
            int remainingCount = BookedModules.Skip(5).Sum(item => item.Count);
            topModules.Add(new BookedModuleStatistics(string.Empty, "Other modules",
                remainingCount, remainingCount * 100d / TotalBookedSessions, "#cbd5dc"));
        }
        TopBookedModules = topModules;
        TopPieGradient = PieGradient(TopBookedModules);
        AllPieGradient = PieGradient(BookedModules);

        var statuses = await periodBookings
            .GroupBy(booking => booking.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Status, item => item.Count, cancellationToken);
        SessionStatuses = Enum.GetValues<BookingStatus>()
            .Select(status => new SessionStatusStatistics(status,
                statuses.GetValueOrDefault(status)))
            .ToList();
        TotalSessionCount = statuses.Values.Sum();
        CompletedSessionCount = statuses.GetValueOrDefault(BookingStatus.Completed);
        CancelledSessionCount = statuses.GetValueOrDefault(BookingStatus.Cancelled);
        DeclinedSessionCount = statuses.GetValueOrDefault(BookingStatus.Declined);
        int resolved = CompletedSessionCount + CancelledSessionCount + DeclinedSessionCount;
        CompletionRate = resolved == 0 ? 0 : CompletedSessionCount * 100d / resolved;

        IQueryable<Booking> completed = periodBookings
            .Where(booking => booking.Status == BookingStatus.Completed);
        PendingStudentReviewCount = await completed
            .CountAsync(booking => booking.StudentEvaluation == null, cancellationToken);
        PendingTutorReviewCount = await completed
            .CountAsync(booking => booking.TutorEvaluation == null, cancellationToken);
        BothReviewsCompleteCount = await completed
            .CountAsync(booking => booking.StudentEvaluation != null &&
                booking.TutorEvaluation != null, cancellationToken);
        AdminReviewsRecordedCount = await completed
            .CountAsync(booking => booking.AdminSessionReview != null, cancellationToken);
        AverageStudentRating = await completed
            .Where(booking => booking.StudentEvaluation != null)
            .AverageAsync(booking => (double?)booking.StudentEvaluation!.ModeRating,
                cancellationToken);

    }

    private static string PieColor(int index)
    {
        string[] palette = ["#a43b70", "#4f87a5", "#df944d", "#6c9b71", "#9273b1"];
        return index < palette.Length ? palette[index] :
            $"hsl({(index * 137.508 % 360).ToString("0.###", CultureInfo.InvariantCulture)} 52% 48%)";
    }

    private static string PieGradient(IReadOnlyList<BookedModuleStatistics> modules)
    {
        if (modules.Count == 0) return "none";
        double end = 0;
        List<string> stops = [];
        for (int index = 0; index < modules.Count; index++)
        {
            double start = end;
            end = index == modules.Count - 1 ? 100 : end + modules[index].Percentage;
            stops.Add($"{modules[index].Color} " +
                $"{start.ToString("0.###", CultureInfo.InvariantCulture)}% " +
                $"{end.ToString("0.###", CultureInfo.InvariantCulture)}%");
        }
        return $"conic-gradient({string.Join(", ", stops)})";
    }
}
