namespace BC_CampusLearn.Models.ViewModels;

public class TutorWeeklySlotViewModel
{
    public DateTimeOffset StartTime { get; set; }

    public TutorWeeklySlotStatus Status { get; set; }

    public int? BookingId { get; set; }

    public string? StudentName { get; set; }

    public string? ModuleCode { get; set; }

    public string? ModuleName { get; set; }

    public string? Location { get; set; }
}

public enum TutorWeeklySlotStatus
{
    Available,
    Booked,
    Active
}
