namespace BC_CampusLearn.Models.ViewModels;

public class BookingPreviewViewModel
{
    public int TutorAvailabilityId { get; set; }

    public int TutorId { get; set; }

    public string TutorName { get; set; } = string.Empty;

    public string ModuleName { get; set; } = string.Empty;

    public DateTimeOffset AvailableTime { get; set; }
}
