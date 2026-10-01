namespace BC_CampusLearn.Models.Entities;

public class SessionAiAssessment
{
    public int SessionAiAssessmentId { get; set; }

    public int BookingId { get; set; }

    public int GeneratedByBcUserId { get; set; }

    public string AssessmentJson { get; set; } = string.Empty;

    public DateTimeOffset GeneratedAt { get; set; }

    public Booking Booking { get; set; } = null!;

    public BcUser GeneratedBy { get; set; } = null!;
}
