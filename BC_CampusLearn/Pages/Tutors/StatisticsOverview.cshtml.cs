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
    public const string DeregistrationConfirmationPhrase = "DEREGISTER MY TUTOR ACCOUNT";

    private static readonly IReadOnlyDictionary<string, int?> RangeMonths =
        new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase)
        {
            ["3m"] = 3,
            ["6m"] = 6,
            ["12m"] = 12,
            ["all"] = null
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
    public string Range { get; set; } = "6m";

    [BindProperty(SupportsGet = true)]
    public bool OpenDeregistration { get; set; }

    [BindProperty]
    public TutorDeregistrationRequestInput DeregistrationInput { get; set; } = new();

    public bool OpenDeregistrationModal { get; private set; }

    public bool HasPendingDeregistrationRequest { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    public string Initials { get; private set; } = string.Empty;

    public string StudentNumber { get; private set; } = string.Empty;

    public string? ProfileImagePath { get; private set; }

    public string PeriodLabel { get; private set; } = string.Empty;

    public TutorStatisticsViewModel Statistics { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(
        CancellationToken cancellationToken)
    {
        if (!RangeMonths.TryGetValue(Range, out int? monthCount))
        {
            Range = "6m";
            monthCount = 6;
        }

        CurrentUser currentUser =
            _currentUserService.GetRequiredUser();

        var tutor = await _context.Tutors
            .AsNoTracking()
            .Where(item => item.BcUserId == currentUser.BcUserId)
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

        HasPendingDeregistrationRequest = await _context.TutorDeregistrationRequests
            .AsNoTracking()
            .AnyAsync(request => request.TutorId == tutor.TutorId &&
                request.Status == TutorAccountRequestStatus.Pending,
                cancellationToken);

        OpenDeregistrationModal = OpenDeregistration &&
            !HasPendingDeregistrationRequest;

        DateTimeOffset localNow = DateTimeOffset.Now;
        DateTimeOffset currentMonth = new(
            localNow.Year,
            localNow.Month,
            1,
            0,
            0,
            0,
            localNow.Offset);
        DateTimeOffset? periodStart = monthCount.HasValue
            ? currentMonth.AddMonths(-(monthCount.Value - 1))
            : null;

        PeriodLabel = monthCount.HasValue
            ? $"Last {monthCount.Value} months"
            : "All time";

        List<StatisticsBookingRow> allBookings =
            await _context.Bookings
                .AsNoTracking()
                .Where(booking => booking.TutorId == tutor.TutorId)
                .Select(booking => new StatisticsBookingRow
                {
                    StudentObjectId = booking.StudentObjectId,
                    StudentTenantId = booking.StudentTenantId,
                    ModuleName = booking.ProgrammeModule.ModuleName,
                    Status = booking.Status,
                    ScheduledStartTime = booking.ScheduledStartTime,
                    DateBooked = booking.DateBooked
                })
                .ToListAsync(cancellationToken);

        List<StatisticsBookingRow> periodBookings = allBookings
            .Where(booking =>
                !periodStart.HasValue ||
                booking.ScheduledStartTime >= periodStart.Value)
            .ToList();

        Statistics = BuildStatistics(
            allBookings,
            periodBookings,
            periodStart,
            monthCount,
            currentMonth);

        return Page();
    }

    public async Task<IActionResult> OnPostRequestDeregistrationAsync(
        CancellationToken cancellationToken)
    {
        OpenDeregistrationModal = true;

        if (!string.Equals(
            DeregistrationInput.ConfirmationText?.Trim(),
            DeregistrationConfirmationPhrase,
            StringComparison.Ordinal))
        {
            ModelState.AddModelError(
                "DeregistrationInput.ConfirmationText",
                $"Type {DeregistrationConfirmationPhrase} exactly as shown.");
        }

        CurrentUser currentUser = _currentUserService.GetRequiredUser();
        int? tutorId = await _context.Tutors
            .Where(item => item.BcUserId == currentUser.BcUserId)
            .Select(item => (int?)item.TutorId)
            .SingleOrDefaultAsync(cancellationToken);
        if (!tutorId.HasValue)
        {
            return Forbid();
        }

        bool alreadyPending = await _context.TutorDeregistrationRequests
            .AnyAsync(request => request.TutorId == tutorId.Value &&
                request.Status == TutorAccountRequestStatus.Pending,
                cancellationToken);
        if (alreadyPending)
        {
            ModelState.AddModelError(
                string.Empty,
                "You already have a tutor deregistration request under review.");
        }

        if (!ModelState.IsValid)
        {
            return await OnGetAsync(cancellationToken);
        }

        _context.TutorDeregistrationRequests.Add(new TutorDeregistrationRequest
        {
            TutorId = tutorId.Value,
            Reason = DeregistrationInput.Reason!.Trim(),
            Status = TutorAccountRequestStatus.Pending,
            SubmittedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync(cancellationToken);

        TempData["DeregistrationRequestSaved"] = true;
        return RedirectToPage(new { Range });
    }

    private static TutorStatisticsViewModel BuildStatistics(
        IReadOnlyCollection<StatisticsBookingRow> allBookings,
        IReadOnlyCollection<StatisticsBookingRow> periodBookings,
        DateTimeOffset? periodStart,
        int? monthCount,
        DateTimeOffset currentMonth)
    {
        List<StatisticsBookingRow> completed = periodBookings
            .Where(booking => booking.Status == BookingStatus.Completed)
            .ToList();
        int cancelled = periodBookings.Count(booking =>
            booking.Status == BookingStatus.Cancelled);
        int concludedAcceptedSessions = completed.Count + cancelled;

        TutorStatisticsViewModel statistics = new()
        {
            CompletedSessions = completed.Count,
            UniqueStudents = completed
                .Select(booking =>
                    $"{booking.StudentTenantId}:{booking.StudentObjectId}")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            TutoringHours = completed.Count,
            CompletionRate = concludedAcceptedSessions == 0
                ? 0
                : Math.Round(
                    completed.Count * 100m / concludedAcceptedSessions,
                    1),
            PendingRequests = allBookings.Count(booking =>
                booking.Status == BookingStatus.Pending)
        };

        int statusTotal = periodBookings.Count;
        statistics.StatusBreakdown = new[]
        {
            (BookingStatus.Completed, "Completed", "completed"),
            (BookingStatus.Confirmed, "Confirmed", "confirmed"),
            (BookingStatus.Pending, "Pending", "pending"),
            (BookingStatus.Cancelled, "Cancelled", "cancelled"),
            (BookingStatus.Declined, "Declined", "declined")
        }
        .Select(item => new TutorStatusStatisticViewModel
        {
            Label = item.Item2,
            CssClass = item.Item3,
            Count = periodBookings.Count(booking =>
                booking.Status == item.Item1),
            Percentage = statusTotal == 0
                ? 0
                : Math.Round(
                    periodBookings.Count(booking =>
                        booking.Status == item.Item1) * 100m / statusTotal,
                    1)
        })
        .ToList();

        var moduleCounts = completed
            .GroupBy(booking => booking.ModuleName)
            .Select(group => new
            {
                ModuleName = group.Key,
                Count = group.Count()
            })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.ModuleName)
            .Take(5)
            .ToList();
        int topModuleCount = moduleCounts.FirstOrDefault()?.Count ?? 0;

        statistics.TopModules = moduleCounts
            .Select(item => new TutorModuleStatisticViewModel
            {
                ModuleName = item.ModuleName,
                SessionCount = item.Count,
                PercentageOfTopModule = topModuleCount == 0
                    ? 0
                    : Math.Round(item.Count * 100m / topModuleCount, 1)
            })
            .ToList();

        statistics.MostRequestedModule = periodBookings
            .Where(booking => booking.Status != BookingStatus.Declined)
            .GroupBy(booking => booking.ModuleName)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => group.Key)
            .FirstOrDefault() ?? "No data yet";

        statistics.BusiestDay = completed
            .GroupBy(booking => booking.ScheduledStartTime.ToLocalTime().DayOfWeek)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => group.Key.ToString())
            .FirstOrDefault() ?? "No data yet";

        List<double> leadTimes = periodBookings
            .Where(booking =>
                (booking.Status == BookingStatus.Confirmed ||
                 booking.Status == BookingStatus.Completed) &&
                booking.ScheduledStartTime > booking.DateBooked)
            .Select(booking =>
                (booking.ScheduledStartTime - booking.DateBooked).TotalDays)
            .ToList();

        statistics.AverageBookingLeadTime = leadTimes.Count == 0
            ? "No data yet"
            : FormatLeadTime(leadTimes.Average());

        BuildTrend(
            statistics,
            completed,
            periodStart,
            monthCount,
            currentMonth);

        return statistics;
    }

    private static void BuildTrend(
        TutorStatisticsViewModel statistics,
        IReadOnlyCollection<StatisticsBookingRow> completed,
        DateTimeOffset? periodStart,
        int? monthCount,
        DateTimeOffset currentMonth)
    {
        DateTimeOffset trendStart;
        int trendMonths;

        if (monthCount.HasValue && periodStart.HasValue)
        {
            trendStart = periodStart.Value;
            trendMonths = monthCount.Value;
        }
        else
        {
            DateTimeOffset? earliest = completed.Count == 0
                ? null
                : completed.Min(booking =>
                    booking.ScheduledStartTime.ToLocalTime());
            DateTimeOffset earliestMonth = earliest.HasValue
                ? new DateTimeOffset(
                    earliest.Value.Year,
                    earliest.Value.Month,
                    1,
                    0,
                    0,
                    0,
                    currentMonth.Offset)
                : currentMonth;
            trendStart = earliestMonth < currentMonth.AddMonths(-11)
                ? currentMonth.AddMonths(-11)
                : earliestMonth;
            trendMonths =
                ((currentMonth.Year - trendStart.Year) * 12) +
                currentMonth.Month - trendStart.Month + 1;
        }

        for (int index = 0; index < trendMonths; index++)
        {
            DateTimeOffset month = trendStart.AddMonths(index);
            statistics.TrendLabels.Add(month.ToString("MMM yyyy"));
            statistics.TrendValues.Add(completed.Count(booking =>
            {
                DateTimeOffset local = booking.ScheduledStartTime.ToLocalTime();
                return local.Year == month.Year && local.Month == month.Month;
            }));
        }
    }

    private static string FormatLeadTime(double days)
    {
        if (days < 1)
        {
            int hours = Math.Max(1, (int)Math.Round(days * 24));
            return $"{hours} {(hours == 1 ? "hour" : "hours")}";
        }

        int roundedDays = Math.Max(1, (int)Math.Round(days));
        return $"{roundedDays} {(roundedDays == 1 ? "day" : "days")}";
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
        public string StudentObjectId { get; set; } = string.Empty;

        public string StudentTenantId { get; set; } = string.Empty;

        public string ModuleName { get; set; } = string.Empty;

        public BookingStatus Status { get; set; }

        public DateTimeOffset ScheduledStartTime { get; set; }

        public DateTimeOffset DateBooked { get; set; }
    }
}
