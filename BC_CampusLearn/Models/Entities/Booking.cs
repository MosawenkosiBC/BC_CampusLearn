namespace BC_CampusLearn.Models.Entities;

public class Booking
{
    public int BookingId { get; set; }

    public int TutorId { get; set; }

    public int ProgrammeModuleId { get; set; }

    public int? StudentBcUserId { get; set; }

    // Identity of the student from Entra claims.
    public string StudentObjectId { get; set; } = string.Empty;

    public string StudentTenantId { get; set; } = string.Empty;

    // Display snapshots.
    public string StudentName { get; set; } = string.Empty;

    public string? StudentEmail { get; set; }

    public string Location { get; set; } = string.Empty;

    public string? Summary { get; set; }

    public string? MeetingLink { get; set; }

    public BookingStatus Status { get; set; }

    public SessionDuration Duration { get; set; }

    public DateTimeOffset ScheduledStartTime { get; set; }

    public DateTimeOffset DateBooked { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ProgrammeModule ProgrammeModule { get; set; } = null!;

    public TutorCourseModule TutorCourseModule { get; set; } = null!;

    public BcUser? StudentBcUser { get; set; }

    public SessionExecution? SessionExecution { get; set; }

    public ICollection<BookingStatusHistory> StatusHistory { get; set; }
        = new List<BookingStatusHistory>();

    public ICollection<SessionMessage> SessionMessages { get; set; }
        = new List<SessionMessage>();

    public ICollection<SessionReview> SessionReviews { get; set; }
        = new List<SessionReview>();

    public TutorStudentEvaluation? TutorEvaluation { get; set; }

    public StudentEvaluation? StudentEvaluation { get; set; }

    public ICollection<BookingPreparationLink> PreparationLinks
    { get; set; } = new List<BookingPreparationLink>();

    public ICollection<BookingDocument> Documents { get; set; }
        = new List<BookingDocument>();
}
