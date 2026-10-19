namespace BC_CampusLearn.Models.Entities;

public class MeetingLink
{
    public int BookingId { get; set; }

    public string Url { get; set; } = string.Empty;

    public Guid? TeamsCreationRequestId { get; set; }

    public string? TeamsEventId { get; set; }

    public string? TeamsOrganizerIdentityHash { get; set; }
    public string? EncryptedTeamsOrganizerTenantId { get; set; }
    public string? EncryptedTeamsOrganizerObjectId { get; set; }
    public bool UsesSharedTeamsOrganizer { get; set; }
    public string? EncryptedTeamsCoOrganizerObjectId { get; set; }
    public DateTimeOffset? TeamsCoOrganizerAssignedAt { get; set; }

    public bool TeamsManualFallbackAllowed { get; set; }

    public DateTimeOffset? TeamsCancellationRequestedAt { get; set; }
    public DateTimeOffset? TeamsCancellationLastAttemptAt { get; set; }
    public DateTimeOffset? TeamsCancelledAt { get; set; }

    public Booking Booking { get; set; } = null!;
}
