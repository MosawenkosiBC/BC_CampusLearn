namespace BC_CampusLearn.Models.Entities;

public class Booking
{
    public int BookingId { get; set; }

    public int TutorId { get; set; }

    public int TutorAvailabilityId { get; set; }

    // Identity of the student from Entra claims.
    public string StudentObjectId { get; set; } = string.Empty;

    public string StudentTenantId { get; set; } = string.Empty;

    // Display snapshots.
    public string StudentName { get; set; } = string.Empty;

    public string? StudentEmail { get; set; }

    public DateTimeOffset SessionStart { get; set; }

    public DateTimeOffset SessionEnd { get; set; }

    public string? Reason { get; set; }

    public BookingStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Tutor Tutor { get; set; } = null!;

    public TutorAvailability TutorAvailability { get; set; } = null!;
}
