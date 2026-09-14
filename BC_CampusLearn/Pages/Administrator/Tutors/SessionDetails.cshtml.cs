using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Administrator.Tutors;

public class SessionDetailsModel(ApplicationDbContext context) : PageModel
{
    public Booking Session { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        var session = await context.Bookings.AsNoTracking()
            .Include(item => item.ProgrammeModule)
            .Include(item => item.StudentEvaluation)
            .Include(item => item.TutorEvaluation)
            .Include(item => item.SessionReviews).ThenInclude(item => item.Reviewer)
            .Include(item => item.TutorCourseModule).ThenInclude(item => item.Tutor).ThenInclude(item => item.BcUser)
            .FirstOrDefaultAsync(item => item.BookingId == id &&
                (item.Status == BookingStatus.Completed || item.Status == BookingStatus.Cancelled), cancellationToken);
        if (session is null) return NotFound();
        Session = session;
        return Page();
    }
}
