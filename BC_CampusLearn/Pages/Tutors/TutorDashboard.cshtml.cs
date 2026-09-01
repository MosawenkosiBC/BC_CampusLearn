using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BC_CampusLearn.Services.Sessions;

namespace BC_CampusLearn.Pages.Tutors;

public class TutorDashboardModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISessionLifecycleService _lifecycleService;

    public TutorDashboardModel(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ISessionLifecycleService lifecycleService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _lifecycleService = lifecycleService;
    }

    public int PendingSessionCount { get; private set; }

    public int UpcomingSessionCount { get; private set; }

    public int OpenSlotCount { get; private set; }

    public int TotalSessionCount { get; private set; }

    public TutorNextSessionViewModel? NextSession
    { get; private set; }

    public DateTimeOffset WeekStart { get; private set; }

    public DateTimeOffset Today { get; private set; }

    public DateTimeOffset MonthStart { get; private set; }

    public DateTimeOffset MonthGridStart { get; private set; }

    public IReadOnlyList<TutorWeeklySlotViewModel> AvailabilitySlots
    { get; private set; } =
        new List<TutorWeeklySlotViewModel>();

    public IReadOnlyList<TutorSessionListItemViewModel> Sessions
    { get; private set; } =
        new List<TutorSessionListItemViewModel>();

    public async Task<IActionResult> OnGetAsync(
        CancellationToken cancellationToken)
    {
        await _lifecycleService.ProcessDueTransitionsAsync(cancellationToken);
        int? tutorId =
            await GetCurrentTutorIdAsync(cancellationToken);

        if (!tutorId.HasValue)
        {
            return Forbid();
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset localNow = DateTimeOffset.Now;
        Today = new DateTimeOffset(
            localNow.Date,
            localNow.Offset);
        int daysSinceMonday =
            ((int)localNow.DayOfWeek + 6) % 7;
        WeekStart = new DateTimeOffset(
            localNow.Date.AddDays(-daysSinceMonday),
            localNow.Offset);
        DateTimeOffset weekEnd = WeekStart.AddDays(7);

        MonthStart = new DateTimeOffset(
            new DateTime(
                localNow.Year,
                localNow.Month,
                1),
            localNow.Offset);
        int monthStartDaysSinceMonday =
            ((int)MonthStart.DayOfWeek + 6) % 7;
        MonthGridStart = MonthStart.AddDays(
            -monthStartDaysSinceMonday);
        DateTimeOffset monthGridEnd =
            MonthGridStart.AddDays(42);
        DateTimeOffset calendarStart =
            WeekStart < MonthGridStart
                ? WeekStart
                : MonthGridStart;
        DateTimeOffset calendarEnd =
            weekEnd > monthGridEnd
                ? weekEnd
                : monthGridEnd;

        var bookingCounts = await _context.Bookings
            .AsNoTracking()
            .Where(booking =>
                booking.TutorId == tutorId.Value)
            .GroupBy(_ => 1)
            .Select(bookings => new
            {
                Pending = bookings.Count(booking =>
                    booking.Status == BookingStatus.Pending),
                Upcoming = bookings.Count(booking =>
                    booking.Status == BookingStatus.Confirmed ||
                    booking.Status == BookingStatus.InProgress),
                Total = bookings.Count(booking =>
                    booking.Status == BookingStatus.Completed)
            })
            .SingleOrDefaultAsync(cancellationToken);

        PendingSessionCount = bookingCounts?.Pending ?? 0;
        UpcomingSessionCount = bookingCounts?.Upcoming ?? 0;
        TotalSessionCount = bookingCounts?.Total ?? 0;

        OpenSlotCount = await _context.TutorAvailabilities
            .AsNoTracking()
            .CountAsync(
                slot =>
                    slot.TutorId == tutorId.Value &&
                    slot.AvailableTime >
                        now,
                cancellationToken);

        NextSession = await _context.Bookings
            .AsNoTracking()
            .Where(booking =>
                booking.TutorId == tutorId.Value &&
                booking.Status == BookingStatus.Confirmed &&
                booking.ScheduledStartTime > now)
            .OrderBy(booking =>
                booking.ScheduledStartTime)
            .Select(booking =>
                new TutorNextSessionViewModel
                {
                    BookingId = booking.BookingId,
                    StudentName = booking.StudentName,
                    ModuleCode =
                        booking.ProgrammeModule.ModuleCode,
                    ModuleName =
                        booking.ProgrammeModule.ModuleName,
                    Location = booking.Location,
                    ScheduledStartTime =
                        booking.ScheduledStartTime,
                    Duration = booking.Duration,
                    MeetingLink = booking.MeetingLink
                })
            .FirstOrDefaultAsync(cancellationToken);

        Sessions = await _context.Bookings
            .AsNoTracking()
            .Where(booking =>
                booking.TutorId == tutorId.Value)
            .OrderBy(booking =>
                booking.Status == BookingStatus.Pending ? 0 : 1)
            .ThenByDescending(booking =>
                booking.DateBooked)
            .Select(booking =>
                new TutorSessionListItemViewModel
                {
                    BookingId = booking.BookingId,
                    StudentName = booking.StudentName,
                    ModuleCode =
                        booking.ProgrammeModule.ModuleCode,
                    Location = booking.Location,
                    ScheduledStartTime =
                        booking.ScheduledStartTime,
                    Duration = booking.Duration,
                    Status = booking.Status
                })
            .Take(5)
            .ToListAsync(cancellationToken);

        List<TutorWeeklySlotViewModel> availableSlots =
            await _context.TutorAvailabilities
                .AsNoTracking()
                .Where(slot =>
                    slot.TutorId == tutorId.Value &&
                    slot.AvailableTime >= calendarStart &&
                    slot.AvailableTime < calendarEnd)
                .Select(slot =>
                    new TutorWeeklySlotViewModel
                    {
                        StartTime = slot.AvailableTime,
                        Status =
                            TutorWeeklySlotStatus.Available
                    })
                .ToListAsync(cancellationToken);

        List<TutorWeeklySlotViewModel> bookedSlots =
            await _context.Bookings
                .AsNoTracking()
                .Where(booking =>
                    booking.TutorId == tutorId.Value &&
                    booking.ScheduledStartTime >= calendarStart &&
                    booking.ScheduledStartTime < calendarEnd &&
                    (booking.Status == BookingStatus.Pending ||
                     booking.Status == BookingStatus.Confirmed ||
                     booking.Status == BookingStatus.InProgress))
                .Select(booking =>
                    new TutorWeeklySlotViewModel
                    {
                        StartTime =
                            booking.ScheduledStartTime,
                        BookingId = booking.BookingId,
                        StudentName = booking.StudentName,
                        ModuleCode =
                            booking.ProgrammeModule.ModuleCode,
                        ModuleName =
                            booking.ProgrammeModule.ModuleName,
                        Location = booking.Location,
                        Status =
                            booking.Status == BookingStatus.Confirmed ||
                            booking.Status == BookingStatus.InProgress
                                ? TutorWeeklySlotStatus.Active
                                : TutorWeeklySlotStatus.Booked
                    })
                .ToListAsync(cancellationToken);

        AvailabilitySlots = availableSlots
            .Concat(bookedSlots)
            .OrderBy(slot => slot.StartTime)
            .ToList();

        return Page();
    }

    public async Task<IActionResult> OnPostUpdateBookingStatusAsync(
        int bookingId,
        CancellationToken cancellationToken)
    {
        int? tutorId =
            await GetCurrentTutorIdAsync(cancellationToken);

        if (!tutorId.HasValue)
        {
            return Forbid();
        }

        bool bookingExists = await _context.Bookings
            .AsNoTracking()
            .AnyAsync(item =>
                item.BookingId == bookingId &&
                item.TutorId == tutorId.Value,
                cancellationToken);

        if (!bookingExists)
        {
            return NotFound();
        }

        return RedirectToPage(
            "/Tutors/SessionDetails",
            new { bookingId });
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
}
