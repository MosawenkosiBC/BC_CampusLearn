namespace BC_CampusLearn.Models.Entities;

public class BcUser
{
    public int BcUserId { get; set; }
    public string PersonnelNumber { get; set; } = null!;
    public Guid EntraObjectId { get; set; }
    public Guid EntraTenantId { get; set; }
    public bool IsPublicActivityEnabled { get; set; } = true;
    public string? PublicActivityDisabledReason { get; set; }
    public DateTime? PublicActivityDisabledAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public Tutor? Tutor { get; set; }
}
