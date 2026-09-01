namespace BC_CampusLearn.Models.Entities;

public class SessionExecution
{
    public int BookingId { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset ExpectedCompletionAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public SessionStartSource StartSource { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Booking Booking { get; set; } = null!;
}
