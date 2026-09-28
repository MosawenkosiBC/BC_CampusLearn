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
    private const string ClosingDateMarker = "Applications close: ";

    public long UserNotificationId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

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

    private bool HasHighlightedClosingDate =>
        Title.EndsWith(
            "Tutor applications are open",
            StringComparison.Ordinal) &&
        Message.Contains(ClosingDateMarker, StringComparison.Ordinal);
}
