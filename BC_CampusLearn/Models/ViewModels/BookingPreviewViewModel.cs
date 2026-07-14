namespace BC_CampusLearn.Models.ViewModels;

public class BookingPreviewViewModel
{
    public int TutorAvailabilityId { get; set; }

    public string TutorName { get; set; } = string.Empty;

    public DateTimeOffset StartTime { get; set; }

    public DateTimeOffset EndTime { get; set; }
}