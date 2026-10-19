namespace BC_CampusLearn.Models.Entities;

public class TutorApplicationSettings
{
    public const int SingletonId = 1;

    public int TutorApplicationSettingsId { get; set; } = SingletonId;
    public bool IsOpen { get; set; }
    public bool SeniorTutorApplicationsOpen { get; set; }
    public DateTime? SeniorTutorOpenDate { get; set; }
    public DateTime? SeniorTutorCloseDate { get; set; }
    public DateTimeOffset? TutorApplicationsNotificationSentAt { get; set; }
    public DateTimeOffset? SeniorTutorApplicationsNotificationSentAt { get; set; }
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

    public bool IsAcceptingSeniorTutorApplications(DateTime utcNow)
    {
        DateTime today = utcNow.Date;
        return SeniorTutorApplicationsOpen &&
            (!SeniorTutorOpenDate.HasValue ||
                SeniorTutorOpenDate.Value.Date <= today) &&
            (!SeniorTutorCloseDate.HasValue ||
                SeniorTutorCloseDate.Value.Date >= today);
    }

    public bool CloseExpiredApplications(DateTime utcNow)
    {
        DateTime today = utcNow.Date;
        bool changed = false;

        if (IsOpen && CloseDate.HasValue &&
            CloseDate.Value.Date < today)
        {
            IsOpen = false;
            NotifyStudents = false;
            changed = true;
        }

        if (SeniorTutorApplicationsOpen &&
            SeniorTutorCloseDate.HasValue &&
            SeniorTutorCloseDate.Value.Date < today)
        {
            SeniorTutorApplicationsOpen = false;
            changed = true;
        }

        if (changed) UpdatedAt = utcNow;
        return changed;
    }

    public int GetApplicationYear(DateTime fallbackDate) =>
        (CloseDate ?? fallbackDate).Year + 1;
}
