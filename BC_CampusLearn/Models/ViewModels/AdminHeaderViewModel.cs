namespace BC_CampusLearn.Models.ViewModels;

public class AdminHeaderViewModel
{
    public string DisplayName { get; set; } = "Administrator";

    public string Initials { get; set; } = "AD";

    public string RoleName { get; set; } = "Admin";

    public bool IsSuperAdmin { get; set; }

    public int PendingTutorApplications { get; set; }

    public int PendingModuleRequests { get; set; }

    public int NotificationCount => (IsSuperAdmin ? PendingTutorApplications : 0) +
        PendingModuleRequests;
}
