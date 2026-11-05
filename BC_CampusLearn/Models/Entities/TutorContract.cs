namespace BC_CampusLearn.Models.Entities;

public class TutorContract
{
    public int Id { get; set; }
    public int TutorId { get; set; }
    // Reference to the signed file in private storage, not a public download URL.
    public string ContractFile { get; set; } = null!;
    public DateTimeOffset DateSubmitted { get; set; }
    public DateTimeOffset? DateChanged { get; set; }
    public Tutor Tutor { get; set; } = null!;
}
