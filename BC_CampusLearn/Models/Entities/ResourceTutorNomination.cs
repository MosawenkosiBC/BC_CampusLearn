namespace BC_CampusLearn.Models.Entities;

public class ResourceTutorNomination
{
    public int TutorId { get; set; }
    public int ProgrammeModuleId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset NominatedAt { get; set; }
    public int NominatedByBcUserId { get; set; }
    public DateTimeOffset? DenominatedAt { get; set; }
    public int? DenominatedByBcUserId { get; set; }

    public Tutor Tutor { get; set; } = null!;
    public ProgrammeModule ProgrammeModule { get; set; } = null!;
}
