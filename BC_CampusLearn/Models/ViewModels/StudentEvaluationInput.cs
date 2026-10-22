using System.ComponentModel.DataAnnotations;

namespace BC_CampusLearn.Models.ViewModels;

public class StudentEvaluationInput
{
    [Range(1, 5)] public byte? ModeRating { get; set; }
    [Required, StringLength(10)] public string TutorResponse { get; set; } = string.Empty;
    [Required, StringLength(10)] public string TutorInterest { get; set; } = string.Empty;
    [Required, StringLength(40)] public string TutorFriendliness { get; set; } = string.Empty;
    [Required, StringLength(40)] public string TutorExplanation { get; set; } = string.Empty;
    [Required, StringLength(40)] public string TutorParticipation { get; set; } = string.Empty;
    [Required, StringLength(40)] public string TutorAdvice { get; set; } = string.Empty;
    [Required, StringLength(50)] public string TutorHelp { get; set; } = string.Empty;
    [Range(1, 5)] public byte? PlatformRating { get; set; }
}
