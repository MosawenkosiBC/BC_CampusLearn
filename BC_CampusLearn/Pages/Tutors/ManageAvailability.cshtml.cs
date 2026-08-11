using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Tutors;

public class ManageAvailabilityModel : PageModel
{
    private const int AvailabilityPageSize = 6;
    private static readonly TimeSpan SouthAfricaOffset =
        TimeSpan.FromHours(2);

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public ManageAvailabilityModel(
        ApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    [BindProperty]
    public DateOnly? SessionDate { get; set; }

    [BindProperty]
    public TimeOnly? SessionTime { get; set; }

    [BindProperty]
    public List<DateOnly> SelectedDates { get; set; } = new();

    [BindProperty]
    public List<TimeOnly> ScheduleTimes { get; set; } = new();

    [BindProperty]
    public List<TimeOnly> SpecificScheduleTimes { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int AvailabilityPage { get; set; } = 1;

    [TempData]
    public bool AvailabilityCreated { get; set; }

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public bool AvailabilityDeleted { get; set; }

    [TempData]
    public string? DeleteSuccessMessage { get; set; }

    public bool CustomAvailabilityModalOpen { get; private set; }

    public int TotalSlots { get; private set; }

    public int WeeklyHours { get; private set; }

    public int MonthlyHours { get; private set; }

    public int TodayAvailability { get; private set; }

    public int SevenDayAvailability { get; private set; }

    public int ThirtyOneDayAvailability { get; private set; }

    public int TotalSlotsBarHeight { get; private set; }

    public int WeeklyHoursBarHeight { get; private set; }

    public int MonthlyHoursBarHeight { get; private set; }

    public string ScheduleRangeLabel { get; private set; } =
        string.Empty;

    public IReadOnlyList<RecurringScheduleDay> RecurringScheduleDays
    { get; private set; } = new List<RecurringScheduleDay>();

    public IReadOnlyList<AvailabilityListItem> AvailabilityRows
    { get; private set; } = new List<AvailabilityListItem>();

    public int TotalAvailabilityCount { get; private set; }

    public int TotalAvailabilityPages { get; private set; }

    public string MinimumDate =>
        DateOnly.FromDateTime(DateTime.Today)
            .ToString("yyyy-MM-dd");

    public async Task<IActionResult> OnGetAsync(
        CancellationToken cancellationToken)
    {
        int? tutorId =
            await GetCurrentTutorIdAsync(cancellationToken);

        if (!tutorId.HasValue)
        {
            return Forbid();
        }

        SessionDate ??=
            DateOnly.FromDateTime(DateTime.Today);

        BuildRecurringScheduleDays();

        await LoadAvailabilityInsightsAsync(
            tutorId.Value,
            cancellationToken);

        return Page();
    }

    public async Task<IActionResult> OnPostCreateAsync(
        CancellationToken cancellationToken)
    {
        int? tutorId =
            await GetCurrentTutorIdAsync(cancellationToken);

        if (!tutorId.HasValue)
        {
            return Forbid();
        }

        await LoadAvailabilityInsightsAsync(
            tutorId.Value,
            cancellationToken);

        BuildRecurringScheduleDays();

        if (!SessionDate.HasValue)
        {
            ModelState.AddModelError(
                nameof(SessionDate),
                "Select a date.");
        }

        if (!SessionTime.HasValue)
        {
            ModelState.AddModelError(
                nameof(SessionTime),
                "Select a time.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        DateTime localDateTime = SessionDate!.Value
            .ToDateTime(
                SessionTime!.Value,
                DateTimeKind.Unspecified);
        DateTimeOffset availableTime = new(
            localDateTime,
            SouthAfricaOffset);

        if (availableTime <= DateTimeOffset.UtcNow)
        {
            ModelState.AddModelError(
                nameof(SessionTime),
                "Choose a future date and time.");
            return Page();
        }

        bool slotExists =
            await _context.TutorAvailabilities
                .AsNoTracking()
                .AnyAsync(
                    slot =>
                        slot.TutorId == tutorId.Value &&
                        slot.AvailableTime == availableTime,
                    cancellationToken);

        if (slotExists)
        {
            ModelState.AddModelError(
                string.Empty,
                "An availability slot already exists for that date and time.");
            return Page();
        }

        _context.TutorAvailabilities.Add(
            new TutorAvailability
            {
                TutorId = tutorId.Value,
                AvailableTime = availableTime
            });

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsUniqueConstraintViolation(exception))
        {
            ModelState.AddModelError(
                string.Empty,
                "An availability slot already exists for that date and time.");
            return Page();
        }

        AvailabilityCreated = true;
        SuccessMessage =
            $"Your availability for {availableTime:dd MMMM yyyy 'at' HH:mm} was created successfully.";

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCreateRecurringAsync(
        CancellationToken cancellationToken)
    {
        int? tutorId =
            await GetCurrentTutorIdAsync(cancellationToken);

        if (!tutorId.HasValue)
        {
            return Forbid();
        }

        BuildRecurringScheduleDays();
        await LoadAvailabilityInsightsAsync(
            tutorId.Value,
            cancellationToken);

        List<DateOnly> selectedDates = SelectedDates
            .Distinct()
            .OrderBy(date => date)
            .ToList();
        List<TimeOnly> scheduleTimes = ScheduleTimes
            .Distinct()
            .OrderBy(time => time)
            .ToList();
        HashSet<DateOnly> allowedDates = RecurringScheduleDays
            .Select(day => day.Date)
            .ToHashSet();

        if (selectedDates.Count == 0)
        {
            ModelState.AddModelError(
                string.Empty,
                "Select at least one schedule date.");
        }
        else if (selectedDates.Any(date =>
                     !allowedDates.Contains(date)))
        {
            ModelState.AddModelError(
                string.Empty,
                "Select dates from the displayed seven-day period.");
        }

        if (scheduleTimes.Count == 0)
        {
            ModelState.AddModelError(
                string.Empty,
                "Add at least one time slot.");
        }
        else if (scheduleTimes.Count > 15)
        {
            ModelState.AddModelError(
                string.Empty,
                "A recurring schedule can contain a maximum of 15 time slots.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        List<DateTimeOffset> candidateTimes = selectedDates
            .SelectMany(date => scheduleTimes.Select(time =>
                new DateTimeOffset(
                    date.ToDateTime(
                        time,
                        DateTimeKind.Unspecified),
                    SouthAfricaOffset)))
            .OrderBy(time => time)
            .ToList();

        if (candidateTimes.Any(time => time <= now))
        {
            ModelState.AddModelError(
                string.Empty,
                "All selected time slots must be in the future.");
            return Page();
        }

        List<DateTimeOffset> existingTimes =
            await _context.TutorAvailabilities
                .AsNoTracking()
                .Where(slot =>
                    slot.TutorId == tutorId.Value &&
                    candidateTimes.Contains(slot.AvailableTime))
                .Select(slot => slot.AvailableTime)
                .ToListAsync(cancellationToken);
        HashSet<DateTimeOffset> existingTimeSet =
            existingTimes.ToHashSet();
        List<DateTimeOffset> newTimes = candidateTimes
            .Where(time => !existingTimeSet.Contains(time))
            .ToList();

        if (newTimes.Count == 0)
        {
            ModelState.AddModelError(
                string.Empty,
                "All selected availability slots already exist.");
            return Page();
        }

        _context.TutorAvailabilities.AddRange(
            newTimes.Select(time =>
                new TutorAvailability
                {
                    TutorId = tutorId.Value,
                    AvailableTime = time
                }));

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsUniqueConstraintViolation(exception))
        {
            ModelState.AddModelError(
                string.Empty,
                "One or more selected availability slots already exist.");
            return Page();
        }

        AvailabilityCreated = true;
        SuccessMessage = newTimes.Count == 1
            ? "1 availability slot was created successfully."
            : $"{newTimes.Count} availability slots were created successfully.";

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAvailabilityAsync(
        int availabilityId,
        CancellationToken cancellationToken)
    {
        int? tutorId =
            await GetCurrentTutorIdAsync(cancellationToken);

        if (!tutorId.HasValue)
        {
            return Forbid();
        }

        TutorAvailability? availability =
            await _context.TutorAvailabilities
                .SingleOrDefaultAsync(slot =>
                    slot.TutorAvailabilityId == availabilityId &&
                    slot.TutorId == tutorId.Value,
                    cancellationToken);

        if (availability is null)
        {
            return NotFound();
        }

        DateTimeOffset localTime =
            availability.AvailableTime.ToOffset(SouthAfricaOffset);

        _context.TutorAvailabilities.Remove(availability);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return RedirectToPage();
        }

        AvailabilityDeleted = true;
        DeleteSuccessMessage =
            $"Availability for {localTime:dd MMMM yyyy 'at' HH:mm} was deleted.";

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCreateForDateAsync(
        CancellationToken cancellationToken)
    {
        int? tutorId =
            await GetCurrentTutorIdAsync(cancellationToken);

        if (!tutorId.HasValue)
        {
            return Forbid();
        }

        await LoadAvailabilityInsightsAsync(
            tutorId.Value,
            cancellationToken);
        BuildRecurringScheduleDays();

        DateOnly today = DateOnly.FromDateTime(
            DateTimeOffset.UtcNow
                .ToOffset(SouthAfricaOffset)
                .Date);
        DateOnly nextMonth = new DateOnly(
            today.Year,
            today.Month,
            1)
            .AddMonths(1);
        List<TimeOnly> times = SpecificScheduleTimes
            .Distinct()
            .OrderBy(time => time)
            .ToList();

        if (!SessionDate.HasValue ||
            SessionDate.Value < today ||
            SessionDate.Value >= nextMonth)
        {
            ModelState.AddModelError(
                nameof(SessionDate),
                "Select an upcoming date in the current month.");
        }

        if (times.Count == 0)
        {
            ModelState.AddModelError(
                nameof(SpecificScheduleTimes),
                "Add at least one time slot.");
        }
        else if (times.Count > 15)
        {
            ModelState.AddModelError(
                nameof(SpecificScheduleTimes),
                "You can add a maximum of 15 time slots.");
        }

        if (!ModelState.IsValid)
        {
            CustomAvailabilityModalOpen = true;
            return Page();
        }

        List<DateTimeOffset> candidateTimes = times
            .Select(time => new DateTimeOffset(
                SessionDate!.Value.ToDateTime(
                    time,
                    DateTimeKind.Unspecified),
                SouthAfricaOffset))
            .Where(time => time > DateTimeOffset.UtcNow)
            .ToList();

        if (candidateTimes.Count != times.Count)
        {
            ModelState.AddModelError(
                nameof(SpecificScheduleTimes),
                "All selected time slots must be in the future.");
            CustomAvailabilityModalOpen = true;
            return Page();
        }

        List<DateTimeOffset> existingTimes =
            await _context.TutorAvailabilities
                .AsNoTracking()
                .Where(slot =>
                    slot.TutorId == tutorId.Value &&
                    candidateTimes.Contains(slot.AvailableTime))
                .Select(slot => slot.AvailableTime)
                .ToListAsync(cancellationToken);
        HashSet<DateTimeOffset> existingTimeSet =
            existingTimes.ToHashSet();
        List<DateTimeOffset> newTimes = candidateTimes
            .Where(time => !existingTimeSet.Contains(time))
            .ToList();

        if (newTimes.Count == 0)
        {
            ModelState.AddModelError(
                string.Empty,
                "All selected availability slots already exist.");
            CustomAvailabilityModalOpen = true;
            return Page();
        }

        _context.TutorAvailabilities.AddRange(
            newTimes.Select(time => new TutorAvailability
            {
                TutorId = tutorId.Value,
                AvailableTime = time
            }));

        await _context.SaveChangesAsync(cancellationToken);

        AvailabilityCreated = true;
        SuccessMessage = newTimes.Count == 1
            ? "1 availability slot was created successfully."
            : $"{newTimes.Count} availability slots were created successfully.";

        return RedirectToPage();
    }

    private async Task LoadAvailabilityInsightsAsync(
        int tutorId,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        List<TutorAvailability> futureAvailability =
            await _context.TutorAvailabilities
                .AsNoTracking()
                .Where(slot =>
                    slot.TutorId == tutorId &&
                    slot.AvailableTime > now)
                .OrderBy(slot => slot.AvailableTime)
                .ToListAsync(cancellationToken);
        List<DateTimeOffset> futureSlots = futureAvailability
            .Select(slot => slot.AvailableTime)
            .ToList();

        TotalAvailabilityCount = futureAvailability.Count;
        TotalAvailabilityPages = (int)Math.Ceiling(
            TotalAvailabilityCount / (double)AvailabilityPageSize);
        AvailabilityPage = TotalAvailabilityPages == 0
            ? 1
            : Math.Clamp(
                AvailabilityPage,
                1,
                TotalAvailabilityPages);

        AvailabilityRows = futureAvailability
            .Skip((AvailabilityPage - 1) * AvailabilityPageSize)
            .Take(AvailabilityPageSize)
            .Select(slot => new AvailabilityListItem
            {
                AvailabilityId = slot.TutorAvailabilityId,
                LocalTime = slot.AvailableTime.ToOffset(SouthAfricaOffset)
            })
            .ToList();

        DateTimeOffset localNow = now.ToOffset(SouthAfricaOffset);
        DateTimeOffset todayStart = new(
            localNow.Date,
            SouthAfricaOffset);
        DateTimeOffset tomorrowStart = todayStart.AddDays(1);
        DateTimeOffset sevenDayEnd = todayStart.AddDays(7);
        DateTimeOffset thirtyOneDayEnd = todayStart.AddDays(31);
        int daysSinceMonday =
            ((int)localNow.DayOfWeek + 6) % 7;
        DateTimeOffset weekStart = new(
            localNow.Date.AddDays(-daysSinceMonday),
            SouthAfricaOffset);
        DateTimeOffset weekEnd = weekStart.AddDays(7);
        DateTimeOffset monthStart = new(
            localNow.Year,
            localNow.Month,
            1,
            0,
            0,
            0,
            SouthAfricaOffset);
        DateTimeOffset monthEnd = monthStart.AddMonths(1);

        TotalSlots = futureSlots.Count;
        WeeklyHours = futureSlots.Count(slot =>
            slot >= weekStart && slot < weekEnd);
        MonthlyHours = futureSlots.Count(slot =>
            slot >= monthStart && slot < monthEnd);
        TodayAvailability = futureSlots.Count(slot =>
            slot >= todayStart && slot < tomorrowStart);
        SevenDayAvailability = futureSlots.Count(slot =>
            slot >= todayStart && slot < sevenDayEnd);
        ThirtyOneDayAvailability = futureSlots.Count(slot =>
            slot >= todayStart && slot < thirtyOneDayEnd);

        int chartMaximum = Math.Max(
            1,
            Math.Max(
                TotalSlots,
                Math.Max(WeeklyHours, MonthlyHours)));
        TotalSlotsBarHeight = GetBarHeight(TotalSlots, chartMaximum);
        WeeklyHoursBarHeight = GetBarHeight(WeeklyHours, chartMaximum);
        MonthlyHoursBarHeight = GetBarHeight(MonthlyHours, chartMaximum);

    }

    private async Task<int?> GetCurrentTutorIdAsync(
        CancellationToken cancellationToken)
    {
        CurrentUser currentUser =
            _currentUserService.GetRequiredUser();

        return await _context.Tutors
            .AsNoTracking()
            .Where(tutor =>
                tutor.BcUserId == currentUser.BcUserId)
            .Select(tutor => (int?)tutor.TutorId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private void BuildRecurringScheduleDays()
    {
        DateOnly today = DateOnly.FromDateTime(
            DateTimeOffset.UtcNow
                .ToOffset(SouthAfricaOffset)
                .Date);
        List<RecurringScheduleDay> days = Enumerable
            .Range(0, 7)
            .Select(offset => today.AddDays(offset))
            .Select(date =>
                new RecurringScheduleDay
                {
                    Date = date,
                    DayLabel = date.ToString("ddd")
                })
            .ToList();

        RecurringScheduleDays = days;
        DateOnly endDate = days[^1].Date;
        ScheduleRangeLabel = today.Month == endDate.Month
            ? $"{today:MMM d} – {endDate.Day}"
            : $"{today:MMM d} – {endDate:MMM d}";
    }

    private static int GetBarHeight(int value, int maximum)
    {
        if (value == 0)
        {
            return 0;
        }

        return Math.Max(
            10,
            (int)Math.Round(value / (double)maximum * 100));
    }

    private static bool IsUniqueConstraintViolation(
        DbUpdateException exception)
    {
        return exception.InnerException is SqlException
        {
            Number: 2601 or 2627
        };
    }
}

public class RecurringScheduleDay
{
    public DateOnly Date { get; set; }

    public string DayLabel { get; set; } = string.Empty;
}

public class AvailabilityListItem
{
    public int AvailabilityId { get; set; }

    public DateTimeOffset LocalTime { get; set; }
}
