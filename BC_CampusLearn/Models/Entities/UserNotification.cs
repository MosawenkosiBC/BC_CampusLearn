namespace BC_CampusLearn.Models.Entities;

public class UserNotification
{
    public long UserNotificationId { get; set; }
    public int RecipientBcUserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string LinkUrl { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public BcUser Recipient { get; set; } = null!;
}
