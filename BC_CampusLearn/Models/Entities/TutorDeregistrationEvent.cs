namespace BC_CampusLearn.Models.Entities;

public class TutorDeregistrationEvent
{
    public int TutorDeregistrationEventId { get; set; }
    public int TutorId { get; set; }
    public DateTimeOffset DeregisteredAt { get; set; }
    public int? AdministratorBcUserId { get; set; }
    public string? AdministratorName { get; set; }
    public string? Reason { get; set; }
    public Tutor Tutor { get; set; } = null!;
}
