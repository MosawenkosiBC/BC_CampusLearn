using BC_CampusLearn.Models.Entities;

namespace BC_CampusLearn.Models.ViewModels;

public class BookingListItemViewModel
{
    public int BookingId { get; set; }

    public string TutorName { get; set; } = string.Empty;

    public DateTimeOffset SessionStart { get; set; }

    public DateTimeOffset SessionEnd { get; set; }

    public BookingStatus Status { get; set; }

    public string? Reason { get; set; }
}