namespace BC_CampusLearn.Models.Entities;

public class SeniorTutorApplication
{
    public int SeniorTutorApplicationId { get; set; }

    public int TutorId { get; set; }

    public decimal AcademicAverage { get; set; }

    public bool IsHeadOfTutorApplication { get; set; }

    public string? BestDescription { get; set; }

    public string? SuitabilityReason { get; set; }

    public TutorAccountRequestStatus Status { get; set; }

    public bool IsArchived { get; set; }

    public DateTime SubmittedAt { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? ReviewedBy { get; set; }

    public string? ReviewNote { get; set; }

    public Tutor Tutor { get; set; } = null!;

    public ICollection<SeniorTutorApplicationModule> RequestedModules { get; set; }
        = new List<SeniorTutorApplicationModule>();
}
