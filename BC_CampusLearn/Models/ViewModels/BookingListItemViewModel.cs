using BC_CampusLearn.Models.Entities;

namespace BC_CampusLearn.Models.ViewModels;

public class BookingListItemViewModel
{
    public int BookingId { get; set; }

    public int TutorId { get; set; }

    public string TutorName { get; set; } = string.Empty;

    public string ModuleName { get; set; } = string.Empty;

    public string ModuleCode { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public DateTimeOffset AvailableTime { get; set; }

    public SessionDuration Duration { get; set; }

    public DateTimeOffset SessionEnd =>
        AvailableTime.AddHours((int)Duration);

    public BookingStatus Status { get; set; }

    public string? Summary { get; set; }
}
