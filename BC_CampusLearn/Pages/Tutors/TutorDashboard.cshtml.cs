using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Tutors;

public class TutorDashboardModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public TutorDashboardModel(
        ApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
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
                    booking.Status == BookingStatus.Confirmed),
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
                    Duration = booking.Duration
                })
            .FirstOrDefaultAsync(cancellationToken);

        Sessions = await _context.Bookings
            .AsNoTracking()
            .Where(booking =>
                booking.TutorId == tutorId.Value)
            .OrderByDescending(booking =>
                booking.ScheduledStartTime)
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
                     booking.Status == BookingStatus.Confirmed))
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
                            booking.Status ==
                                BookingStatus.Confirmed
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
        BookingStatus status,
        CancellationToken cancellationToken)
    {
        if (status != BookingStatus.Confirmed &&
            status != BookingStatus.Cancelled &&
            status != BookingStatus.Declined)
        {
            return BadRequest();
        }

        int? tutorId =
            await GetCurrentTutorIdAsync(cancellationToken);

        if (!tutorId.HasValue)
        {
            return Forbid();
        }

        var booking = await _context.Bookings
            .AsNoTracking()
            .Where(item =>
                item.BookingId == bookingId &&
                item.TutorId == tutorId.Value)
            .Select(item => new
            {
                item.ScheduledStartTime,
                item.Status
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (booking is null)
        {
            return NotFound();
        }

        if (booking.Status != BookingStatus.Pending)
        {
            return RedirectToPage();
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                cancellationToken);

        int updatedCount = await _context.Bookings
            .Where(item =>
                item.BookingId == bookingId &&
                item.TutorId == tutorId.Value &&
                item.Status == BookingStatus.Pending)
            .ExecuteUpdateAsync(
                updates => updates.SetProperty(
                    item => item.Status,
                    status),
                cancellationToken);

        if (updatedCount == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return RedirectToPage();
        }

        bool shouldRestoreAvailability =
            (status == BookingStatus.Cancelled ||
             status == BookingStatus.Declined) &&
            booking.ScheduledStartTime >
                DateTimeOffset.UtcNow;

        if (shouldRestoreAvailability)
        {
            DateTimeOffset conflictRangeStart =
                booking.ScheduledStartTime.AddHours(-1);
            DateTimeOffset conflictRangeEnd =
                booking.ScheduledStartTime.AddHours(1);
            bool availabilityExists =
                await _context.TutorAvailabilities
                    .AnyAsync(
                        slot =>
                            slot.TutorId == tutorId.Value &&
                            slot.AvailableTime > conflictRangeStart &&
                            slot.AvailableTime < conflictRangeEnd,
                        cancellationToken);
            bool activeBookingExists =
                await _context.Bookings
                    .AsNoTracking()
                    .AnyAsync(
                        item =>
                            item.BookingId != bookingId &&
                            item.TutorId == tutorId.Value &&
                            item.Status != BookingStatus.Cancelled &&
                            item.Status != BookingStatus.Declined &&
                            item.ScheduledStartTime > conflictRangeStart &&
                            item.ScheduledStartTime < conflictRangeEnd,
                        cancellationToken);

            if (!availabilityExists && !activeBookingExists)
            {
                _context.TutorAvailabilities.Add(
                    new TutorAvailability
                    {
                        TutorId = tutorId.Value,
                        AvailableTime =
                            booking.ScheduledStartTime
                    });

                await _context.SaveChangesAsync(
                    cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);

        return RedirectToPage();
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
