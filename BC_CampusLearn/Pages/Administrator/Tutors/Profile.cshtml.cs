using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Administrator.Tutors;

public class ProfileModel(ApplicationDbContext context) : PageModel
{
    public Tutor Tutor { get; private set; } = null!;
    public int CompletedSessions { get; private set; }
    public int ModuleChangeRequests { get; private set; }
    public int PendingStudentReviews { get; private set; }
    [BindProperty(SupportsGet = true)]
    public string Period { get; set; } = "month";
    public string PeriodLabel { get; private set; } = string.Empty;
    public double? AverageRating { get; private set; }
    public int ReviewCount { get; private set; }
    public IReadOnlyList<ModuleCompletion> TopModules { get; private set; } = [];
    public sealed record ModuleCompletion(string Code, string Name, int Count);
    public IReadOnlyList<Booking> RecentSessions { get; private set; } = [];
    [BindProperty(SupportsGet = true)]
    public int SessionPage { get; set; } = 1;
    public int TotalSessionPages { get; private set; }
    public const int SessionPageSize = 5;

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        var tutor = await context.Tutors
            .AsNoTracking()
            .Include(item => item.BcUser)
            .Include(item => item.Programme)
            .Include(item => item.TutorCourseModules)
                .ThenInclude(item => item.ProgrammeModule)
            .FirstOrDefaultAsync(item => item.TutorId == id, cancellationToken);

        if (tutor is null)
        {
            return NotFound();
        }

        Tutor = tutor;
        var bookings = context.Bookings.AsNoTracking().Where(item => item.TutorId == id);
        CompletedSessions = await bookings.CountAsync(item => item.Status == BookingStatus.Completed, cancellationToken);
        PendingStudentReviews = await bookings.CountAsync(item => item.Status == BookingStatus.Completed && item.StudentEvaluation == null, cancellationToken);
        ModuleChangeRequests = await context.TutorModuleChangeRequests.CountAsync(item => item.TutorId == id && item.Status == TutorAccountRequestStatus.Pending, cancellationToken);
        var now = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(2));
        Period = Period?.ToLowerInvariant() is "day" or "week" or "month" ? Period.ToLowerInvariant() : "month";
        var today = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset);
        var periodStart = Period switch
        {
            "day" => today,
            "week" => today.AddDays(-((int)today.DayOfWeek + 6) % 7),
            _ => new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, now.Offset)
        };
        var periodEnd = Period switch
        {
            "day" => periodStart.AddDays(1),
            "week" => periodStart.AddDays(7),
            _ => periodStart.AddMonths(1)
        };
        PeriodLabel = Period switch
        {
            "day" => now.ToString("dd MMMM yyyy"),
            "week" => $"{periodStart:dd MMM yyyy} – {periodEnd.AddDays(-1):dd MMM yyyy}",
            _ => now.ToString("MMMM yyyy")
        };
        var ratings = await context.SessionReviews.AsNoTracking()
            .Where(item => item.Booking.TutorId == id && item.RevieweeBcUserId == tutor.BcUserId
                && item.CreatedAt >= periodStart && item.CreatedAt < periodEnd)
            .Select(item => (double)item.Rating).ToListAsync(cancellationToken);
        ReviewCount = ratings.Count;
        AverageRating = ratings.Count > 0 ? ratings.Average() : null;
        var topModules = await bookings
            .Where(item => item.Status == BookingStatus.Completed
                && item.CompletedAt >= periodStart && item.CompletedAt < periodEnd)
            .GroupBy(item => new { item.ProgrammeModuleId, item.ProgrammeModule.ModuleCode, item.ProgrammeModule.ModuleName })
            .Select(group => new { Code = group.Key.ModuleCode, Name = group.Key.ModuleName, Count = group.Count() })
            .OrderByDescending(item => item.Count).ThenBy(item => item.Code)
            .Take(5).ToListAsync(cancellationToken);
        TopModules = topModules.Select(item => new ModuleCompletion(item.Code, item.Name, item.Count)).ToList();
        var visibleSessions = bookings.Where(item => item.Status == BookingStatus.Completed || item.Status == BookingStatus.Cancelled);
        var sessionCount = await visibleSessions.CountAsync(cancellationToken);
        TotalSessionPages = Math.Max(1, (int)Math.Ceiling(sessionCount / (double)SessionPageSize));
        SessionPage = Math.Clamp(SessionPage, 1, TotalSessionPages);
        RecentSessions = await visibleSessions.Include(item => item.ProgrammeModule)
            .OrderByDescending(item => item.ScheduledStartTime).ThenByDescending(item => item.BookingId)
            .Skip((SessionPage - 1) * SessionPageSize)
            .Take(SessionPageSize).ToListAsync(cancellationToken);
        return Page();
    }
}
