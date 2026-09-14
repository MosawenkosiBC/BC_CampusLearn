namespace BC_CampusLearn.Models.Entities;

public class Admin
{
    public int AdminId { get; set; }
    public int BcUserId { get; set; }
    public DateTime CreatedAt { get; set; }

    public BcUser BcUser { get; set; } = null!;
}
