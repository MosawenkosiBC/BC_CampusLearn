namespace BC_CampusLearn.Models.Entities;

public enum AnnouncementAudience { TutorsOnly = 1, AllStudents = 2 }

public class Announcement
{
    public Guid AnnouncementId { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public AnnouncementAudience Audience { get; set; }
    public int SenderBcUserId { get; set; }
    public BcUser Sender { get; set; } = null!;
    public DateTimeOffset SentAt { get; set; }
    public ICollection<UserNotification> Notifications { get; set; } = new List<UserNotification>();
}
