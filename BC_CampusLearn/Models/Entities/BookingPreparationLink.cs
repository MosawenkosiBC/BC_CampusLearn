namespace BC_CampusLearn.Models.Entities;

public class BookingPreparationLink
{
    public int BookingPreparationLinkId { get; set; }

    public int BookingId { get; set; }

    public byte Position { get; set; }

    public string Url { get; set; } = string.Empty;

    public Booking Booking { get; set; } = null!;
}
