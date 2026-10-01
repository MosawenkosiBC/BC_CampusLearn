namespace BC_CampusLearn.Models.Entities;

public class BcUser
{
    public int BcUserId { get; set; }
    public string? PersonnelNumber { get; set; }
    public string? EncryptedEntraTenantId { get; set; }
    public string? EncryptedEntraObjectId { get; set; }
    public string? EntraIdentityLookupHash { get; set; }
    public string? EncryptedGeminiApiKey { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public BcUserRole Role { get; set; } = BcUserRole.Student;
    public bool IsAdministrativeAccessActive { get; set; } = true;
    public bool IsPublicActivityEnabled { get; set; } = true;
    public string? PublicActivityDisabledReason { get; set; }
    public DateTime? PublicActivityDisabledAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTimeOffset? SessionReviewsLastViewedAt { get; set; }
    public Tutor? Tutor { get; set; }
    public Admin? Admin { get; set; }
    public ICollection<ResourceComment> ResourceComments { get; set; }
        = new List<ResourceComment>();
    public ICollection<UserNotification> Notifications { get; set; }
        = new List<UserNotification>();
    public ICollection<TutorApplicationReviewDecision>
        TutorApplicationReviewDecisions { get; set; }
        = new List<TutorApplicationReviewDecision>();
    public ICollection<SettingAuditLog> SettingAuditLogs { get; set; }
        = new List<SettingAuditLog>();
}
