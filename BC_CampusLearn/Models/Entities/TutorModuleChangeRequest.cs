namespace BC_CampusLearn.Models.Entities;

public class TutorModuleChangeRequest
{
    public int TutorModuleChangeRequestId { get; set; }

    public int TutorId { get; set; }

    public int ProgrammeModuleId { get; set; }

    public TutorModuleChangeRequestType RequestType { get; set; }

    public TutorAccountRequestStatus Status { get; set; }

    public string? Reason { get; set; }

    public DateTime SubmittedAt { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public Tutor Tutor { get; set; } = null!;

    public ProgrammeModule ProgrammeModule { get; set; } = null!;
}
