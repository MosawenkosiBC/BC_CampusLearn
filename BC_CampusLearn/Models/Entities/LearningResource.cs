namespace BC_CampusLearn.Models.Entities;

public class LearningResource
{
    public int LearningResourceId { get; set; }
    public int TutorId { get; set; }
    public int ProgrammeModuleId { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool AllowSubscriberComments { get; set; }
    public string? Link1 { get; set; }
    public string? Link2 { get; set; }
    public LearningResourceStatus Status { get; set; }
    public DateTimeOffset DateCreated { get; set; }
    public DateTimeOffset? DatePublished { get; set; }
    public DateTimeOffset? DateUpdated { get; set; }
    public DateTime? TutorLastViewedDiscussionAt { get; set; }

    public Tutor Tutor { get; set; } = null!;
    public ProgrammeModule ProgrammeModule { get; set; } = null!;
    public ICollection<LearningResourceDocument> Documents { get; set; }
        = new List<LearningResourceDocument>();
    public ICollection<ResourceComment> Comments { get; set; }
        = new List<ResourceComment>();
}
