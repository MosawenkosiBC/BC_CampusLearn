using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Messages;

[Authorize]
public class OpenModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly TimeProvider _timeProvider;

    public OpenModel(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        TimeProvider timeProvider)
    {
        _context = context;
        _currentUserService = currentUserService;
        _timeProvider = timeProvider;
    }

    public async Task<IActionResult> OnGetAsync(
        long messageId,
        CancellationToken cancellationToken)
    {
        CurrentUser user = _currentUserService.GetRequiredUser();
        SessionMessage? message = await _context.SessionMessages
            .Include(item => item.Booking)
                .ThenInclude(booking => booking.TutorCourseModule)
                    .ThenInclude(assignment => assignment.Tutor)
            .SingleOrDefaultAsync(item =>
                item.SessionMessageId == messageId &&
                item.RecipientBcUserId == user.BcUserId &&
                item.DeletedAt == null,
                cancellationToken);

        if (message is null)
        {
            return NotFound();
        }

        if (!message.ReadAt.HasValue)
        {
            message.ReadAt = _timeProvider.GetUtcNow();
            await _context.SaveChangesAsync(cancellationToken);
        }

        bool recipientIsTutor =
            message.Booking.TutorCourseModule.Tutor.BcUserId ==
            user.BcUserId;
        string pageName = recipientIsTutor
            ? "/Tutors/SessionDetails"
            : "/Bookings/SessionDetails";
        string? pageUrl = Url.Page(
            pageName,
            values: new { bookingId = message.BookingId });

        return LocalRedirect(
            $"{pageUrl ?? "/"}#message-{message.SessionMessageId}");
    }
}
