using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
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

    public int AvailableTutorCount { get; private set; }

    public IReadOnlyList<BookingListItemViewModel>
        UpcomingBookings
    { get; private set; }
        = new List<BookingListItemViewModel>();

    public async Task OnGetAsync()
    {
        CurrentUser =
            _currentUserService.GetRequiredUser();

        AvailableTutorCount =
            await _context.Tutors.CountAsync(
                tutor =>
                    tutor.IsApproved &&
                    tutor.IsActive);

        UpcomingBookings =
            await _context.Bookings
                .AsNoTracking()
                .Where(booking =>
                    booking.StudentObjectId ==
                        CurrentUser.ObjectId &&
                    booking.StudentTenantId ==
                        CurrentUser.TenantId &&
                    booking.SessionStart >
                        DateTimeOffset.UtcNow &&
                    booking.Status !=
                        Models.Entities.BookingStatus.Cancelled)
                .OrderBy(booking =>
                    booking.SessionStart)
                .Select(booking =>
                    new BookingListItemViewModel
                    {
                        BookingId =
                            booking.BookingId,

                        TutorName =
                            booking.Tutor.DisplayName,

                        SessionStart =
                            booking.SessionStart,

                        SessionEnd =
                            booking.SessionEnd,

                        Status = booking.Status,

                        Reason = booking.Reason
                    })
                .Take(3)
                .ToListAsync();
    }
}