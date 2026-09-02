using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.ViewComponents;

public class MessageNotificationsViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public MessageNotificationsViewComponent(
        ApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<IViewComponentResult> InvokeAsync(string instance)
    {
        if (!_currentUserService.IsAuthenticated)
        {
            return Content(string.Empty);
        }

        CurrentUser user = _currentUserService.GetRequiredUser();
        IQueryable<Models.Entities.SessionMessage> unread =
            _context.SessionMessages
                .AsNoTracking()
                .Where(message =>
                    message.RecipientBcUserId == user.BcUserId &&
                    message.ReadAt == null &&
                    message.DeletedAt == null);

        int unreadCount = await unread.CountAsync();
        List<MessageNotificationItemViewModel> messages = await unread
            .OrderByDescending(message => message.SentAt)
            .Take(10)
            .Select(message => new MessageNotificationItemViewModel
            {
                SessionMessageId = message.SessionMessageId,
                BookingId = message.BookingId,
                SenderName = string.IsNullOrWhiteSpace(
                    message.Sender.DisplayName)
                    ? message.Sender.PersonnelNumber
                    : message.Sender.DisplayName,
                MessageText = message.MessageText,
                SentAt = message.SentAt
            })
            .ToListAsync();

        return View(
            "~/Pages/Shared/Components/MessageNotifications/Default.cshtml",
            new MessageNotificationMenuViewModel
            {
                Instance = instance,
                UnreadCount = unreadCount,
                Messages = messages
            });
    }
}
