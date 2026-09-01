namespace BC_CampusLearn.Models.Entities;

public class BookingStatusHistory
{
    public int BookingStatusHistoryId { get; set; }

    public int BookingId { get; set; }

    public BookingStatus PreviousStatus { get; set; }

    public BookingStatus NewStatus { get; set; }

    public string? ReasonCode { get; set; }

    public string? Reason { get; set; }

    public int? ChangedByBcUserId { get; set; }

    public bool ChangedBySystem { get; set; }

    public bool AvailabilityReopened { get; set; }

    public DateTimeOffset ChangedAt { get; set; }

    public Booking Booking { get; set; } = null!;

    public BcUser? ChangedByBcUser { get; set; }
}
