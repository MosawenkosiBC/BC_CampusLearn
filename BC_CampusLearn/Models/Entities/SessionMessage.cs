namespace BC_CampusLearn.Models.Entities;

public class SessionMessage
{
    public long SessionMessageId { get; set; }

    public int BookingId { get; set; }

    public int SenderBcUserId { get; set; }

    public string MessageText { get; set; } = string.Empty;

    public DateTimeOffset SentAt { get; set; }

    public DateTimeOffset? EditedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public Booking Booking { get; set; } = null!;

    public BcUser Sender { get; set; } = null!;
}
