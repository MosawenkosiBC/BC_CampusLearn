namespace BC_CampusLearn.Models.Entities;

public class SeniorTutorApplication
{
    public int SeniorTutorApplicationId { get; set; }

    public int TutorId { get; set; }

    public decimal AcademicAverage { get; set; }

    public string BestDescription { get; set; } = null!;

    public string SuitabilityReason { get; set; } = null!;

    public TutorAccountRequestStatus Status { get; set; }

    public DateTime SubmittedAt { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? ReviewedBy { get; set; }

    public string? ReviewNote { get; set; }

    public Tutor Tutor { get; set; } = null!;
}
