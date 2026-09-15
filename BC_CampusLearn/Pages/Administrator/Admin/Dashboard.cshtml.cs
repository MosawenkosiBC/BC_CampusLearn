using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Administrator.Admin;

public class DashboardModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public DashboardModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public int ActiveTutorCount { get; private set; }
    public int TutorResourceCount { get; private set; }
    public int PendingAdminRequestCount { get; private set; }
    public int CompletedSessionCount { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        ActiveTutorCount = await _context.Tutors
            .AsNoTracking()
            .CountAsync(
                tutor =>
                    tutor.Status == TutorStatus.Approved &&
                    tutor.IsActive,
                cancellationToken);

        TutorResourceCount = await _context.LearningResources
            .AsNoTracking()
            .CountAsync(cancellationToken);

        int pendingTutorApplications = await _context.Tutors
            .AsNoTracking()
            .CountAsync(
                tutor => tutor.Status == TutorStatus.Pending,
                cancellationToken);

        int pendingModuleRequests = await _context.TutorModuleChangeRequests
            .AsNoTracking()
            .CountAsync(
                request => request.Status == TutorAccountRequestStatus.Pending,
                cancellationToken);

        int pendingDeregistrationRequests = await _context.TutorDeregistrationRequests
            .AsNoTracking()
            .CountAsync(
                request => request.Status == TutorAccountRequestStatus.Pending,
                cancellationToken);

        PendingAdminRequestCount = pendingTutorApplications +
            pendingModuleRequests +
            pendingDeregistrationRequests;

        CompletedSessionCount = await _context.Bookings
            .AsNoTracking()
            .CountAsync(
                booking => booking.Status == BookingStatus.Completed,
                cancellationToken);
    }
}
