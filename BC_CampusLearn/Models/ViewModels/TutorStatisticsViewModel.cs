namespace BC_CampusLearn.Models.ViewModels;

public class TutorStatisticsViewModel
{
    public int CompletedSessions { get; set; }

    public int UniqueStudents { get; set; }

    public int TutoringHours { get; set; }

    public decimal CompletionRate { get; set; }

    public int PendingRequests { get; set; }

    public string MostRequestedModule { get; set; } = "No data yet";

    public string BusiestDay { get; set; } = "No data yet";

    public string AverageBookingLeadTime { get; set; } = "No data yet";

    public List<string> TrendLabels { get; set; } = new();

    public List<int> TrendValues { get; set; } = new();

    public List<TutorStatusStatisticViewModel> StatusBreakdown { get; set; }
        = new();

    public List<TutorModuleStatisticViewModel> TopModules { get; set; }
        = new();
}

public class TutorStatusStatisticViewModel
{
    public string Label { get; set; } = string.Empty;

    public string CssClass { get; set; } = string.Empty;

    public int Count { get; set; }

    public decimal Percentage { get; set; }
}

public class TutorModuleStatisticViewModel
{
    public string ModuleName { get; set; } = string.Empty;

    public int SessionCount { get; set; }

    public decimal PercentageOfTopModule { get; set; }
}
