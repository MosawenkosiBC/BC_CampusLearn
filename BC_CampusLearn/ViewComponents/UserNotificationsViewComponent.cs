using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.ViewComponents;

public class UserNotificationsViewComponent(
    ApplicationDbContext context,
    ICurrentUserService currentUserService) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(string instance)
    {
        if (!currentUserService.IsAuthenticated)
        {
            return Content(string.Empty);
        }

        CurrentUser user = currentUserService.GetRequiredUser();
        var unread = context.UserNotifications
            .AsNoTracking()
            .Where(item => item.RecipientBcUserId == user.BcUserId &&
                item.ReadAt == null);

        int unreadCount = await unread.CountAsync();
        List<UserNotificationItemViewModel> notifications = await unread
            .OrderByDescending(item => item.CreatedAt)
            .Take(10)
            .Select(item => new UserNotificationItemViewModel
            {
                UserNotificationId = item.UserNotificationId,
                Title = item.Title,
                Message = item.Message,
                CreatedAt = item.CreatedAt
            })
            .ToListAsync();

        return View(
            "~/Pages/Shared/Components/UserNotifications/Default.cshtml",
            new UserNotificationMenuViewModel
            {
                Instance = instance,
                UnreadCount = unreadCount,
                Notifications = notifications
            });
    }
}
