namespace BC_CampusLearn.Models.Entities;

public class TutorStudentEvaluation
{
    public int TutorEvaluationId { get; set; }

    public int BookingId { get; set; }

    public bool SessionPlan { get; set; }

    public bool StudentPreparationInfo { get; set; }

    public bool StudentPunctuality { get; set; }

    public bool StudentPrepared { get; set; }

    public string? PreviousHomework { get; set; }

    public string StudentInteract { get; set; } = string.Empty;

    public string StudentFocus { get; set; } = string.Empty;

    public string StudentIssues { get; set; } = string.Empty;

    public string TutorComments { get; set; } = string.Empty;

    public string RecordingLink { get; set; } = string.Empty;

    public Booking Booking { get; set; } = null!;
}
