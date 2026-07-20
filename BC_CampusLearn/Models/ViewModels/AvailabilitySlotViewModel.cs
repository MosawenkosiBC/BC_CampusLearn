namespace BC_CampusLearn.Models.ViewModels;

public class AvailabilitySlotViewModel
{
    public int TutorAvailabilityId { get; set; }

    public string ModuleCode { get; set; } = string.Empty;

    public string ModuleName { get; set; } = string.Empty;

    public DateTimeOffset AvailableTime { get; set; }
}
