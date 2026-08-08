using BC_CampusLearn.Models.Entities;

namespace BC_CampusLearn.Models.ViewModels;

public class TutorSessionListItemViewModel
{
    public int BookingId { get; set; }

    public string StudentName { get; set; } = string.Empty;

    public string ModuleCode { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public DateTimeOffset ScheduledStartTime { get; set; }

    public SessionDuration Duration { get; set; }

    public DateTimeOffset SessionEnd =>
        ScheduledStartTime.AddHours((int)Duration);

    public BookingStatus Status { get; set; }
}
