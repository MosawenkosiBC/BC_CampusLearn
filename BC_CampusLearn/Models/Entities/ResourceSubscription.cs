namespace BC_CampusLearn.Models.Entities;

public class ResourceSubscription
{
    public int ResourceSubscriptionId { get; set; }
    public string PersonnelNumber { get; set; } = string.Empty;
    public string ModuleCode { get; set; } = string.Empty;
    public DateTimeOffset DateSubscribed { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset? LastAccessedAt { get; set; }
}
