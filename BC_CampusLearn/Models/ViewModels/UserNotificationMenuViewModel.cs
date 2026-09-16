namespace BC_CampusLearn.Models.ViewModels;

public class UserNotificationMenuViewModel
{
    public string Instance { get; set; } = string.Empty;
    public int UnreadCount { get; set; }
    public IReadOnlyList<UserNotificationItemViewModel> Notifications
    { get; set; } = Array.Empty<UserNotificationItemViewModel>();
}

public class UserNotificationItemViewModel
{
    public long UserNotificationId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
