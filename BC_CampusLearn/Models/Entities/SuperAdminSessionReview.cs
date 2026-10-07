namespace BC_CampusLearn.Models.Entities;

public class SuperAdminSessionReview
{
    public int SuperAdminSessionReviewId { get; set; }

    public int BookingId { get; set; }

    public int ReviewerBcUserId { get; set; }

    public bool IsAccepted { get; set; }

    public decimal? CompensationAmount { get; set; }

    public DateTimeOffset RecordedAt { get; set; }

    public Booking Booking { get; set; } = null!;

    public BcUser Reviewer { get; set; } = null!;
}
