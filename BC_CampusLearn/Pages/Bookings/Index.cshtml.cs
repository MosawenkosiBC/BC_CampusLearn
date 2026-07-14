using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Bookings;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public IndexModel(
        ApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public IReadOnlyList<BookingListItemViewModel>
        Bookings
    { get; private set; }
        = new List<BookingListItemViewModel>();

    public async Task OnGetAsync(
        CancellationToken cancellationToken)
    {
        CurrentUser student =
            _currentUserService.GetRequiredUser();

        Bookings = await _context.Bookings
            .AsNoTracking()
            .Where(booking =>
                booking.StudentObjectId ==
                    student.ObjectId &&
                booking.StudentTenantId ==
                    student.TenantId)
            .OrderByDescending(booking =>
                booking.SessionStart)
            .Select(booking =>
                new BookingListItemViewModel
                {
                    BookingId = booking.BookingId,

                    TutorName =
                        booking.Tutor.DisplayName,

                    SessionStart =
                        booking.SessionStart,

                    SessionEnd =
                        booking.SessionEnd,

                    Status = booking.Status,

                    Reason = booking.Reason
                })
            .ToListAsync(cancellationToken);
    }
}