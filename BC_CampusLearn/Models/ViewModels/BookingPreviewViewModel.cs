namespace BC_CampusLearn.Models.ViewModels;

public class BookingPreviewViewModel
{
    public int TutorAvailabilityId { get; set; }

    public int TutorId { get; set; }

    public string TutorName { get; set; } = string.Empty;

    public List<BookingModuleOptionViewModel> Modules { get; set; } = new();

    public DateTimeOffset AvailableTime { get; set; }
}
