namespace BC_CampusLearn.Models.Entities;

public class StudentEvaluation
{
    public int StudentEvaluationId { get; set; }
    public int BookingId { get; set; }
    public string TutoringMode { get; set; } = string.Empty;
    public string PlatformExperience { get; set; } = string.Empty;
    public byte ModeRating { get; set; }
    public string TutorResponse { get; set; } = string.Empty;
    public string TutorInterest { get; set; } = string.Empty;
    public string TutorFriendliness { get; set; } = string.Empty;
    public string TutorExplanation { get; set; } = string.Empty;
    public string TutorParticipation { get; set; } = string.Empty;
    public string TutorPunctuality { get; set; } = string.Empty;
    public string TutorAdvice { get; set; } = string.Empty;
    public string TutorHelp { get; set; } = string.Empty;
    public string TutorTopic { get; set; } = string.Empty;
    public string TutoringService { get; set; } = string.Empty;
    public string ImproveBCProgramme { get; set; } = string.Empty;
    public byte PlatformRating { get; set; }
    public Booking Booking { get; set; } = null!;
}
