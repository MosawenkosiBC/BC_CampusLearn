namespace BC_CampusLearn.Models.Entities;

public class StudyArea
{
    public int StudyAreaId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Booking> Bookings { get; set; }
        = new List<Booking>();
}
