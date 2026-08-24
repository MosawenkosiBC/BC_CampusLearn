namespace BC_CampusLearn.Models.Entities;

public class LearningResourceDocument
{
    public int ResourceDocumentId { get; set; }
    public int ResourceId { get; set; }
    public string DocumentName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTimeOffset DateUploaded { get; set; }

    public LearningResource Resource { get; set; } = null!;
}
