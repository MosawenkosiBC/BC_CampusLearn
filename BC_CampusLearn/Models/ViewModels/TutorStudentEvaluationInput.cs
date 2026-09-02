using System.ComponentModel.DataAnnotations;

namespace BC_CampusLearn.Models.ViewModels;

public class TutorStudentEvaluationInput
{
    [Required(ErrorMessage = "Select yes or no.")]
    public bool? SessionPlan { get; set; }

    [Required(ErrorMessage = "Select yes or no.")]
    public bool? StudentPreparationInfo { get; set; }

    [Required(ErrorMessage = "Select yes or no.")]
    public bool? StudentPunctuality { get; set; }

    [Required(ErrorMessage = "Select yes or no.")]
    public bool? StudentPrepared { get; set; }

    [StringLength(250)]
    public string? PreviousHomework { get; set; }

    [Required]
    [StringLength(250)]
    public string StudentInteract { get; set; } = string.Empty;

    [Required]
    [StringLength(250)]
    public string StudentFocus { get; set; } = string.Empty;

    [Required]
    [StringLength(250)]
    public string StudentIssues { get; set; } = string.Empty;

    [Required]
    public string TutorComments { get; set; } = string.Empty;

    [Required]
    [StringLength(2048)]
    [Url(ErrorMessage = "Enter a valid recording URL.")]
    public string RecordingLink { get; set; } = string.Empty;
}
