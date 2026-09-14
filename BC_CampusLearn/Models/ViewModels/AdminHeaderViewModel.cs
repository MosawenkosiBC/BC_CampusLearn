namespace BC_CampusLearn.Models.ViewModels;

public class AdminHeaderViewModel
{
    public string DisplayName { get; set; } = "Administrator";

    public string Initials { get; set; } = "AD";

    public string RoleName { get; set; } = "Admin";

    public int PendingTutorApplications { get; set; }

    public int PendingModuleRequests { get; set; }

    public int PendingDeregistrationRequests { get; set; }

    public int NotificationCount => PendingTutorApplications +
        PendingModuleRequests +
        PendingDeregistrationRequests;
}
