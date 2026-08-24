namespace BC_CampusLearn.Models.Entities;

public class ResourceComment
{
    public int CommentId { get; set; }
    public int ResourceId { get; set; }
    public int AuthorUserId { get; set; }
    public int? ParentCommentId { get; set; }
    public string CommentText { get; set; } = string.Empty;
    public DateTime DateCreated { get; set; }
    public bool IsEdited { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsPinned { get; set; }

    public LearningResource Resource { get; set; } = null!;
    public BcUser Author { get; set; } = null!;
    public ResourceComment? ParentComment { get; set; }
    public ICollection<ResourceComment> Replies { get; set; }
        = new List<ResourceComment>();
}
