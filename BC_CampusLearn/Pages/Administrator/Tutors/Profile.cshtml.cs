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
    public int PendingTutorReviews { get; private set; }
    [BindProperty(SupportsGet = true)]
    public string Period { get; set; } = "month";
    [BindProperty(SupportsGet = true)]
    public DateOnly? StartDate { get; set; }
    [BindProperty(SupportsGet = true)]
    public DateOnly? EndDate { get; set; }
    public string? DateFilterError { get; private set; }
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
            .Include(item => item.TutorCourseModules.Where(a => a.IsActive))
                .ThenInclude(item => item.ProgrammeModule)
            .FirstOrDefaultAsync(item => item.TutorId == id, cancellationToken);

        if (tutor is null)
        {
            return NotFound();
        }

        Tutor = tutor;
        var bookings = context.Bookings.AsNoTracking().Where(item => item.TutorId == id);
        var now = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(2));
        Period = Period?.ToLowerInvariant() is "day" or "week" or "month" or "custom" ? Period.ToLowerInvariant() : "month";
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
        DateFilterError = null;
        if (Period == "custom")
        {
            if (!ModelState.IsValid || !StartDate.HasValue || !EndDate.HasValue
                || StartDate > EndDate || StartDate <= DateOnly.MinValue || EndDate >= DateOnly.MaxValue)
            {
                DateFilterError = "Choose valid start and end dates, with the end on or after the start. Showing this month until a valid range is applied.";
            }
            else
            {
                periodStart = new DateTimeOffset(StartDate.Value.ToDateTime(TimeOnly.MinValue), now.Offset);
                periodEnd = new DateTimeOffset(EndDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), now.Offset);
            }
        }
        else
        {
            StartDate = DateOnly.FromDateTime(periodStart.DateTime);
            EndDate = DateOnly.FromDateTime(periodEnd.AddDays(-1).DateTime);
        }
        PeriodLabel = Period switch
        {
            "custom" when DateFilterError is null => $"{periodStart:dd MMM yyyy} – {periodEnd.AddDays(-1):dd MMM yyyy}",
            "day" => now.ToString("dd MMMM yyyy"),
            "week" => $"{periodStart:dd MMM yyyy} – {periodEnd.AddDays(-1):dd MMM yyyy}",
            _ => now.ToString("MMMM yyyy")
        };
        // Use the session date displayed in the table; completion can be recorded later.
        var periodBookings = bookings.Where(item => item.ScheduledStartTime >= periodStart
            && item.ScheduledStartTime < periodEnd);
        var completedBookings = periodBookings.Where(item => item.Status == BookingStatus.Completed);
        CompletedSessions = await completedBookings.CountAsync(cancellationToken);
        PendingStudentReviews = await completedBookings.CountAsync(item => item.StudentEvaluation == null, cancellationToken);
        PendingTutorReviews = await completedBookings.CountAsync(item => item.TutorEvaluation == null, cancellationToken);
        var submittedStart = periodStart.UtcDateTime;
        var submittedEnd = periodEnd.UtcDateTime;
        ModuleChangeRequests = await context.TutorModuleChangeRequests.CountAsync(item => item.TutorId == id
            && item.Status == TutorAccountRequestStatus.Pending
            && item.SubmittedAt >= submittedStart && item.SubmittedAt < submittedEnd, cancellationToken);
        var ratings = await completedBookings
            .Where(item => item.StudentEvaluation != null)
            .Select(item => (double)item.StudentEvaluation!.ModeRating)
            .ToListAsync(cancellationToken);
        ReviewCount = ratings.Count;
        AverageRating = ratings.Count > 0 ? ratings.Average() : null;
        var topModules = await completedBookings
            .GroupBy(item => new { item.ProgrammeModuleId, item.ProgrammeModule.ModuleCode, item.ProgrammeModule.ModuleName })
            .Select(group => new { Code = group.Key.ModuleCode, Name = group.Key.ModuleName, Count = group.Count() })
            .OrderByDescending(item => item.Count).ThenBy(item => item.Code)
            .Take(5).ToListAsync(cancellationToken);
        TopModules = topModules.Select(item => new ModuleCompletion(item.Code, item.Name, item.Count)).ToList();
        var visibleSessions = periodBookings.Where(item => item.Status == BookingStatus.Completed || item.Status == BookingStatus.Cancelled);
        var sessionCount = await visibleSessions.CountAsync(cancellationToken);
        TotalSessionPages = Math.Max(1, (int)Math.Ceiling(sessionCount / (double)SessionPageSize));
        SessionPage = Math.Clamp(SessionPage, 1, TotalSessionPages);
        // Tutor Head reviews are not persisted yet, so the three-review tier is currently empty.
        RecentSessions = await visibleSessions
            .Include(item => item.ProgrammeModule)
            .Include(item => item.AdminSessionReview)
            .OrderByDescending(item => item.StudentEvaluation != null && item.TutorEvaluation != null)
            .ThenByDescending(item => item.StudentEvaluation != null || item.TutorEvaluation != null)
            .ThenByDescending(item => item.ScheduledStartTime).ThenByDescending(item => item.BookingId)
            .Skip((SessionPage - 1) * SessionPageSize)
            .Take(SessionPageSize).ToListAsync(cancellationToken);
        return Page();
    }
}
