namespace BC_CampusLearn.Models.Entities;

public class TutorApplicationSettings
{
    public const int SingletonId = 1;

    public int TutorApplicationSettingsId { get; set; } = SingletonId;
    public bool IsOpen { get; set; }
    public bool NotifyStudents { get; set; }
    public int? ShortlistLimit { get; set; }
    public DateTime? OpenDate { get; set; }
    public DateTime? CloseDate { get; set; }
    public bool ContinueAfterShortlistLimit { get; set; }
    public DateTime UpdatedAt { get; set; }

    public bool IsAcceptingApplications(DateTime utcNow)
    {
        DateTime today = utcNow.Date;
        return IsOpen &&
            (!OpenDate.HasValue || OpenDate.Value.Date <= today) &&
            (!CloseDate.HasValue || CloseDate.Value.Date >= today);
    }
}
