using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Notifications;

[Authorize]
public class OpenModel(
    ApplicationDbContext context,
    ICurrentUserService currentUserService,
    TimeProvider timeProvider) : PageModel
{
    public async Task<IActionResult> OnGetAsync(
        long notificationId,
        CancellationToken cancellationToken)
    {
        CurrentUser user = currentUserService.GetRequiredUser();
        UserNotification? notification = await context.UserNotifications
            .SingleOrDefaultAsync(item =>
                item.UserNotificationId == notificationId &&
                item.RecipientBcUserId == user.BcUserId,
                cancellationToken);

        if (notification is null)
        {
            return NotFound();
        }

        if (!notification.ReadAt.HasValue)
        {
            notification.ReadAt = timeProvider.GetUtcNow();
            await context.SaveChangesAsync(cancellationToken);
        }

        return Url.IsLocalUrl(notification.LinkUrl)
            ? LocalRedirect(notification.LinkUrl)
            : LocalRedirect("/Student/Dashboard");
    }
}
