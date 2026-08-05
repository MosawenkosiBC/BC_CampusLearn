namespace BC_CampusLearn.Models.Entities;

public class TutorAvailability
{
    public int TutorAvailabilityId { get; set; }
    // Foreign key to the Tutor entity.

    public int TutorId { get; set; }

    public DateTimeOffset AvailableTime { get; set; }

    // Used to detect two students trying to book the same slot.
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Tutor Tutor { get; set; } = null!;

}
