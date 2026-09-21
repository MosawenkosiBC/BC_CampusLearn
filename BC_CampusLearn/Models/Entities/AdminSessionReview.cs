namespace BC_CampusLearn.Models.Entities;

public class AdminSessionReview
{
    public int AdminSessionReviewId { get; set; }

    public int BookingId { get; set; }

    public int ReviewerBcUserId { get; set; }

    public bool AllReviewsSubmitted { get; set; }

    public bool HeadConfirmedSession { get; set; }

    public bool HeadConfirmedQuality { get; set; }

    public bool ConcernsResolvedOrDocumented { get; set; }

    public bool EvidenceSupportsApproval { get; set; }

    public DateTimeOffset RecordedAt { get; set; }

    public Booking Booking { get; set; } = null!;

    public BcUser Reviewer { get; set; } = null!;
}
