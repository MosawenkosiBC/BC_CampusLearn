namespace BC_CampusLearn.Models.Entities;

public class EmailNotificationOutbox
{
    public long EmailNotificationOutboxId { get; set; }
    public string EventKey { get; set; } = string.Empty;
    public string RecipientEmail { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? ActionPath { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? FailedAt { get; set; }
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
}
