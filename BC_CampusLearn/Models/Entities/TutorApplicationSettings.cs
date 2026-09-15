namespace BC_CampusLearn.Models.Entities;

public class TutorApplicationSettings
{
    public const int SingletonId = 1;

    public int TutorApplicationSettingsId { get; set; } = SingletonId;
    public bool IsOpen { get; set; }
    public bool NotifyStudents { get; set; }
    public int? ShortlistLimit { get; set; }
    public DateTime UpdatedAt { get; set; }
}
