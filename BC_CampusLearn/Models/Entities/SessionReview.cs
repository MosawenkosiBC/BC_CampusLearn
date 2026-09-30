namespace BC_CampusLearn.Models.Entities;

public class SessionReview
{
    public int SessionReviewId { get; set; }

    public int BookingId { get; set; }

    public int ReviewerBcUserId { get; set; }

    public int? RevieweeBcUserId { get; set; }

    public byte Rating { get; set; }

    public string? Comment { get; set; }

    public string? ModuleAndTopicCoverage { get; set; }

    public string? ExplanationClarity { get; set; }

    public string? SessionStructure { get; set; }

    public string? StudentEngagement { get; set; }

    public string? EvidenceConsistency { get; set; }

    public string? ConcernLevel { get; set; }

    public string? OverallAssessment { get; set; }

    public string? Decision { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Booking Booking { get; set; } = null!;

    public BcUser Reviewer { get; set; } = null!;

    public BcUser? Reviewee { get; set; }
}
