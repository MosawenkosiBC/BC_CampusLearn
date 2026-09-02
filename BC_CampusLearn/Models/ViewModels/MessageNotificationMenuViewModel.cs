namespace BC_CampusLearn.Models.ViewModels;

public class MessageNotificationMenuViewModel
{
    public string Instance { get; set; } = string.Empty;

    public int UnreadCount { get; set; }

    public IReadOnlyList<MessageNotificationItemViewModel> Messages
    { get; set; } = Array.Empty<MessageNotificationItemViewModel>();
}

public class MessageNotificationItemViewModel
{
    public long SessionMessageId { get; set; }

    public int BookingId { get; set; }

    public string SenderName { get; set; } = string.Empty;

    public string MessageText { get; set; } = string.Empty;

    public DateTimeOffset SentAt { get; set; }
}
