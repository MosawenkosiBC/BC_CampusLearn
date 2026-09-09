namespace BC_CampusLearn.Models.Entities;

public class MeetingLink
{
    public int BookingId { get; set; }

    public string Url { get; set; } = string.Empty;

    public Booking Booking { get; set; } = null!;
}
