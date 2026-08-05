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

        DateTimeOffset now = DateTimeOffset.UtcNow;

        await _context.Bookings
            .Where(booking =>
                booking.Status ==
                    Models.Entities.BookingStatus.Pending &&
                booking.ScheduledStartTime <= now)
            .ExecuteUpdateAsync(
                updates => updates.SetProperty(
                    booking => booking.Status,
                    Models.Entities.BookingStatus.Declined),
                cancellationToken);

        Bookings = await _context.Bookings
            .AsNoTracking()
            .Where(booking =>
                booking.StudentObjectId ==
                    student.ObjectId &&
                booking.StudentTenantId ==
                    student.TenantId)
            .OrderByDescending(booking =>
                booking.ScheduledStartTime)
            .Select(booking =>
                new BookingListItemViewModel
                {
                    BookingId = booking.BookingId,

                    TutorName =
                        booking.TutorCourseModule
                            .Tutor.BcUser.PersonnelNumber,

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
            .ToListAsync(cancellationToken);
    }
}
