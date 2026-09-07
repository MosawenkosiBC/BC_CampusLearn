namespace BC_CampusLearn.Models.ViewModels;

public class TutorStatisticsViewModel
{
    public int CompletedSessions { get; set; }

    public int UniqueStudents { get; set; }

    public int TutoringHours { get; set; }

    public decimal CompletionRate { get; set; }

    public int PendingRequests { get; set; }

    public int PendingStudentReviews { get; set; }

    public int PendingTutorReviews { get; set; }

    public decimal AverageStudentReviewRating { get; set; }

    public List<TutorModuleStatisticViewModel> TopModules { get; set; }
        = new();
}

public class TutorModuleStatisticViewModel
{
    public string ModuleCode { get; set; } = string.Empty;

    public int SessionCount { get; set; }

}
