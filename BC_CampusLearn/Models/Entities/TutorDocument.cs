namespace BC_CampusLearn.Models.Entities;

public class TutorDocument
{
    public int TutorDocumentId { get; set; }
    public int TutorId { get; set; }
    public TutorDocumentType DocumentType { get; set; }
    public string FilePath { get; set; } = null!;
    public string OriginalFileName { get; set; } = null!;
    public bool IsVerified { get; set; }
    public DateTime UploadedAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public Tutor Tutor { get; set; } = null!;
}
