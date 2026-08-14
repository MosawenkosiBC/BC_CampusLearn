using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Student;

[Authorize]
public class DashboardModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public DashboardModel(
        ApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public CurrentUser CurrentUser { get; private set; }
        = null!;

    public DashboardSummaryViewModel Summary { get; private set; }
        = new();

    public BookingListItemViewModel? NextSession
    { get; private set; }

    public IReadOnlyList<BookingListItemViewModel>
        Sessions
    { get; private set; }
        = new List<BookingListItemViewModel>();

    public async Task OnGetAsync(
        CancellationToken cancellationToken)
    {
        CurrentUser =
            _currentUserService.GetRequiredUser();

        DateTimeOffset now = DateTimeOffset.UtcNow;

        await _context.Bookings
            .Where(booking =>
                booking.Status == BookingStatus.Pending &&
                booking.ScheduledStartTime <= now)
            .ExecuteUpdateAsync(
                updates => updates.SetProperty(
                    booking => booking.Status,
                    BookingStatus.Declined),
                cancellationToken);

        IQueryable<Booking> studentBookings =
            _context.Bookings
                .AsNoTracking()
                .Where(booking =>
                    booking.StudentObjectId ==
                        CurrentUser.ObjectId &&
                    booking.StudentTenantId ==
                        CurrentUser.TenantId);

        Summary =
            await studentBookings
                .GroupBy(_ => 1)
                .Select(bookings =>
                    new DashboardSummaryViewModel
                    {
                        UpcomingSessionCount =
                            bookings.Count(booking =>
                                booking.Status ==
                                    BookingStatus.Confirmed &&
                                booking.ScheduledStartTime > now),

                        PendingSessionCount =
                            bookings.Count(booking =>
                                booking.Status ==
                                    BookingStatus.Pending),

                        CompletedSessionCount =
                            bookings.Count(booking =>
                                booking.Status ==
                                    BookingStatus.Completed),

                        CancelledSessionCount =
                            bookings.Count(booking =>
                                booking.Status ==
                                    BookingStatus.Cancelled ||
                                booking.Status ==
                                    BookingStatus.Declined)
                    })
                .FirstOrDefaultAsync(cancellationToken)
            ?? new DashboardSummaryViewModel();

        NextSession =
            await studentBookings
                .Where(booking =>
                    booking.ScheduledStartTime > now &&
                    booking.Status == BookingStatus.Confirmed)
                .OrderBy(booking =>
                    booking.ScheduledStartTime)
                .Select(booking =>
                    new BookingListItemViewModel
                    {
                        BookingId =
                            booking.BookingId,

                        TutorId = booking.TutorId,

                        TutorName = string.IsNullOrWhiteSpace(
                            booking.TutorCourseModule.Tutor.BcUser.DisplayName)
                            ? booking.TutorCourseModule.Tutor.BcUser.PersonnelNumber
                            : booking.TutorCourseModule.Tutor.BcUser.DisplayName,

                        ModuleName =
                            booking.ProgrammeModule.ModuleName,

                        ModuleCode =
                            booking.ProgrammeModule.ModuleCode,

                        Location = booking.Location,

                        AvailableTime =
                            booking.ScheduledStartTime,

                        Duration = booking.Duration,

                        Status = booking.Status,

                        Summary = booking.Summary
                    })
                .FirstOrDefaultAsync(cancellationToken);

        Sessions =
            await studentBookings
                .OrderByDescending(booking =>
                    booking.ScheduledStartTime)
                .Select(booking =>
                    new BookingListItemViewModel
                    {
                        BookingId = booking.BookingId,

                        TutorId = booking.TutorId,

                        TutorName = string.IsNullOrWhiteSpace(
                            booking.TutorCourseModule.Tutor.BcUser.DisplayName)
                            ? booking.TutorCourseModule.Tutor.BcUser.PersonnelNumber
                            : booking.TutorCourseModule.Tutor.BcUser.DisplayName,

                        ModuleName =
                            booking.ProgrammeModule.ModuleName,

                        ModuleCode =
                            booking.ProgrammeModule.ModuleCode,

                        Location = booking.Location,

                        AvailableTime =
                            booking.ScheduledStartTime,

                        Duration = booking.Duration,

                        Status = booking.Status,

                        Summary = booking.Summary
                    })
                .Take(5)
                .ToListAsync(cancellationToken);

    }
}
