namespace BC_CampusLearn.Models.Entities;

public class SettingAuditLog
{
    public long SettingAuditLogId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string SettingName { get; set; } = string.Empty;
    public string? PreviousValue { get; set; }
    public string? NewValue { get; set; }
    public int ChangedByBcUserId { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
    public string? Reason { get; set; }

    public BcUser ChangedBy { get; set; } = null!;
}
