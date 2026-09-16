namespace BC_CampusLearn.Models.Entities;

public class TutorApplicationReviewDecision
{
    public int TutorApplicationReviewDecisionId { get; set; }
    public int TutorId { get; set; }
    public int ReviewerBcUserId { get; set; }
    public string AdminName { get; set; } = null!;
    public TutorApplicationStage PreviousStage { get; set; }
    public TutorApplicationStage NewStage { get; set; }
    public string Reason { get; set; } = null!;
    public DateTime ReviewedAt { get; set; }

    public Tutor Tutor { get; set; } = null!;
    public BcUser Reviewer { get; set; } = null!;
}
