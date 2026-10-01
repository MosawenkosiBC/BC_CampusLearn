namespace BC_CampusLearn.Models.Entities;

public class CampusEvent
{
    public int CampusEventId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Disclaimer { get; set; }
    public string Location { get; set; } = string.Empty;
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public DateTimeOffset PublishAt { get; set; }
    public bool IsPublished { get; set; }
    public string BannerImagePath { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public ICollection<CampusEventDetail> Details { get; set; }
        = new List<CampusEventDetail>();
}
