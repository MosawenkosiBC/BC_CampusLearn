namespace BC_CampusLearn.Models.ViewModels;

public class DashboardSummaryViewModel
{
    public int UpcomingSessionCount { get; set; }

    public int PendingSessionCount { get; set; }

    public int CompletedSessionCount { get; set; }

    public int CancelledSessionCount { get; set; }
}
