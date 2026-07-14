namespace BC_CampusLearn.Models.ViewModels;

public class AvailabilitySlotViewModel
{
    public int TutorAvailabilityId { get; set; }

    public DateTimeOffset StartTime { get; set; }

    public DateTimeOffset EndTime { get; set; }
}