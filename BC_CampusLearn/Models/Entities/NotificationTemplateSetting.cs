namespace BC_CampusLearn.Models.Entities;

public class NotificationTemplateSetting
{
    public string TemplateKey { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
    public int UpdatedByBcUserId { get; set; }
    public BcUser UpdatedBy { get; set; } = null!;
}
