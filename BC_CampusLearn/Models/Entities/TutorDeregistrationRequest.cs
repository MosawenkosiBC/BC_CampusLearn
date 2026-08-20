namespace BC_CampusLearn.Models.Entities;

public class TutorDeregistrationRequest
{
    public int TutorDeregistrationRequestId { get; set; }
    public int TutorId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public TutorAccountRequestStatus Status { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Tutor Tutor { get; set; } = null!;
}
