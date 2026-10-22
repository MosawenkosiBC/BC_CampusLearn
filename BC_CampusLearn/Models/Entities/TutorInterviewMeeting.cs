namespace BC_CampusLearn.Models.Entities;

// Retain the reservation even if an application is deleted, so its invitation
// can still be cancelled. TutorId + SubmittedAt identifies one application cycle.
public sealed class TutorInterviewMeeting
{
    public Guid TutorInterviewMeetingId { get; set; }
    public int TutorId { get; set; }
    public DateTime ApplicationSubmittedAt { get; set; }
    public string OrganizerObjectId { get; set; } = "";
    public string RequestJson { get; set; } = "";
    public string? EventId { get; set; }
    public string? JoinUrl { get; set; }
    public DateTimeOffset ScheduledStart { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public int CreatedByBcUserId { get; set; }
    public Guid Version { get; set; }
}
