namespace BC_CampusLearn.Models.Entities;

public class SessionReview
{
    public int SessionReviewId { get; set; }

    public int BookingId { get; set; }

    public int ReviewerBcUserId { get; set; }

    public int? RevieweeBcUserId { get; set; }

    public byte Rating { get; set; }

    public string? Comment { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Booking Booking { get; set; } = null!;

    public BcUser Reviewer { get; set; } = null!;

    public BcUser? Reviewee { get; set; }
}
