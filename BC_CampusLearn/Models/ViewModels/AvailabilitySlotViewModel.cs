namespace BC_CampusLearn.Models.ViewModels;

public class AvailabilitySlotViewModel
{
    public int TutorAvailabilityId { get; set; }

    public DateTimeOffset AvailableTime { get; set; }

    public bool IsBooked { get; set; }
}
