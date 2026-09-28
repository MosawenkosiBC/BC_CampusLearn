using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Notifications;

[Authorize]
public class IndexModel(
    ApplicationDbContext context,
    ICurrentUserService currentUserService,
    TimeProvider timeProvider) : PageModel
{
    private const int PageSize = 8;

    [BindProperty(SupportsGet = true)]
    public long? NotificationId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;

    public int UnreadCount { get; private set; }
    public int TotalPages { get; private set; }
    public NotificationItem? SelectedNotification { get; private set; }
    public IReadOnlyList<NotificationItem> Notifications { get; private set; }
        = Array.Empty<NotificationItem>();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        CurrentUser user = currentUserService.GetRequiredUser();
        if (NotificationId.HasValue)
        {
            Models.Entities.UserNotification? selected = await context
                .UserNotifications
                .SingleOrDefaultAsync(item =>
                    item.UserNotificationId == NotificationId.Value &&
                    item.RecipientBcUserId == user.BcUserId,
                    cancellationToken);

            if (selected is not null)
            {
                if (!selected.ReadAt.HasValue)
                {
                    selected.ReadAt = timeProvider.GetUtcNow();
                    await context.SaveChangesAsync(cancellationToken);
                }

                SelectedNotification = MapNotification(selected);
            }
        }

        var notifications = context.UserNotifications
            .AsNoTracking()
            .Where(item => item.RecipientBcUserId == user.BcUserId);

        int totalCount = await notifications.CountAsync(cancellationToken);
        UnreadCount = await notifications.CountAsync(
            item => item.ReadAt == null,
            cancellationToken);
        TotalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));
        CurrentPage = Math.Clamp(CurrentPage, 1, TotalPages);

        Notifications = await notifications
            .OrderByDescending(item => item.CreatedAt)
            .Skip((CurrentPage - 1) * PageSize)
            .Take(PageSize)
            .Select(item => new NotificationItem(
                item.UserNotificationId,
                item.Title,
                item.Message,
                item.LinkUrl,
                item.CreatedAt,
                item.ReadAt))
            .ToListAsync(cancellationToken);

    }

    public async Task<IActionResult> OnPostMarkAllReadAsync(
        int currentPage,
        CancellationToken cancellationToken)
    {
        CurrentUser user = currentUserService.GetRequiredUser();
        List<Models.Entities.UserNotification> unread = await context
            .UserNotifications
            .Where(item => item.RecipientBcUserId == user.BcUserId &&
                item.ReadAt == null)
            .ToListAsync(cancellationToken);
        DateTimeOffset readAt = timeProvider.GetUtcNow();

        foreach (Models.Entities.UserNotification notification in unread)
        {
            notification.ReadAt = readAt;
        }

        await context.SaveChangesAsync(cancellationToken);
        return RedirectToPage(new { CurrentPage = Math.Max(1, currentPage) });
    }

    private static NotificationItem MapNotification(
        Models.Entities.UserNotification notification) =>
        new(
            notification.UserNotificationId,
            notification.Title,
            notification.Message,
            notification.LinkUrl,
            notification.CreatedAt,
            notification.ReadAt);

    public sealed record NotificationItem(
        long NotificationId,
        string Title,
        string Message,
        string LinkUrl,
        DateTimeOffset CreatedAt,
        DateTimeOffset? ReadAt)
    {
        private const string ClosingDateMarker = "Applications close: ";

        public string SummaryMessage => HasHighlightedClosingDate
            ? Message[..Message.LastIndexOf(
                ClosingDateMarker,
                StringComparison.Ordinal)].TrimEnd()
            : Message;

        public string? HighlightedClosingDate => HasHighlightedClosingDate
            ? Message[(Message.LastIndexOf(
                ClosingDateMarker,
                StringComparison.Ordinal) + ClosingDateMarker.Length)..]
            : null;

        public string PreviewMessage => SummaryMessage
            .Split('\n', 2, StringSplitOptions.None)[0];

        public string ActionLabel =>
            Title.EndsWith(
                "Tutor applications are open",
                StringComparison.Ordinal) &&
            LinkUrl.Equals(
                "/Tutors/TutorApplication",
                StringComparison.OrdinalIgnoreCase)
                ? "Apply now"
                : Title == "Session completed" &&
            IsSessionLink
                ? "Complete review"
                : IsSessionLink
                    ? "View session"
                    : LinkUrl.Equals(
                        "/Tutors/TutorApplication",
                        StringComparison.OrdinalIgnoreCase)
                        ? "View application"
                        : LinkUrl.Equals(
                            "/Tutors/PublicProfile",
                            StringComparison.OrdinalIgnoreCase) ||
                          LinkUrl.Equals(
                            "/Tutors/Profile",
                            StringComparison.OrdinalIgnoreCase)
                            ? "View profile"
                        : "View related page";

        private bool HasHighlightedClosingDate =>
            Title.EndsWith(
                "Tutor applications are open",
                StringComparison.Ordinal) &&
            Message.Contains(ClosingDateMarker, StringComparison.Ordinal);

        private bool IsSessionLink =>
            LinkUrl.StartsWith(
                "/Bookings/SessionDetails/",
                StringComparison.OrdinalIgnoreCase) ||
            LinkUrl.StartsWith(
                "/Tutors/SessionDetails/",
                StringComparison.OrdinalIgnoreCase);
    }
}
