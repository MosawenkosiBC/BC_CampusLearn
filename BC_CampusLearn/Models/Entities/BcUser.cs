namespace BC_CampusLearn.Models.Entities;

public class BcUser
{
    public int BcUserId { get; set; }
    public string PersonnelNumber { get; set; } = null!;
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public BcUserRole Role { get; set; } = BcUserRole.Student;
    public bool IsPublicActivityEnabled { get; set; } = true;
    public string? PublicActivityDisabledReason { get; set; }
    public DateTime? PublicActivityDisabledAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public Tutor? Tutor { get; set; }
    public Admin? Admin { get; set; }
    public ICollection<ResourceComment> ResourceComments { get; set; }
        = new List<ResourceComment>();
    public ICollection<UserNotification> Notifications { get; set; }
        = new List<UserNotification>();
}
