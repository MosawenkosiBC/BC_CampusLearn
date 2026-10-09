using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Announcements;

[Authorize]
public class DetailsModel(ApplicationDbContext context,
    ICurrentUserService currentUserService, TimeProvider timeProvider) : PageModel
{
    public Announcement Announcement { get; private set; } = null!;
    public async Task<IActionResult> OnGetAsync(Guid announcementId, CancellationToken cancellationToken)
    {
        var user = currentUserService.GetRequiredUser();
        bool isAdmin = user.Role is BcUserRole.Admin or BcUserRole.SuperAdmin or BcUserRole.Dev;
        var announcement = await context.Announcements.AsNoTracking().Include(item => item.Sender)
            .SingleOrDefaultAsync(item => item.AnnouncementId == announcementId &&
                (isAdmin || item.Notifications.Any(notification => notification.RecipientBcUserId == user.BcUserId)),
                cancellationToken);
        if (announcement is null) return NotFound();
        Announcement = announcement;
        var notification = await context.UserNotifications.SingleOrDefaultAsync(item =>
            item.AnnouncementId == announcementId && item.RecipientBcUserId == user.BcUserId, cancellationToken);
        if (notification is not null && notification.ReadAt is null)
        {
            notification.ReadAt = timeProvider.GetUtcNow();
            await context.SaveChangesAsync(cancellationToken);
        }
        return Page();
    }
}
