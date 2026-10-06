using System.ComponentModel.DataAnnotations;

namespace BC_CampusLearn.Models.ViewModels;

public class TutorHeadSessionReviewInput : IValidatableObject
{
    [Required(ErrorMessage = "Select whether the tutor engaged the student appropriately.")]
    [RegularExpression("^(Yes|Partially|No)$", ErrorMessage = "Select a valid student engagement response.")]
    public string? StudentEngagement { get; set; }

    [Required(ErrorMessage = "Select whether any concerns were identified.")]
    [RegularExpression("^(No Concerns|Concerns)$", ErrorMessage = "Select a valid concern level.")]
    public string? ConcernLevel { get; set; }

    [Required(ErrorMessage = "Select an overall session assessment.")]
    [RegularExpression("^(Excellent|Good|Satisfactory|Needs improvement)$", ErrorMessage = "Select a valid overall assessment.")]
    public string? OverallAssessment { get; set; }

    [Required(ErrorMessage = "Select a decision for this session.")]
    [RegularExpression("^(Approve|Reject|Escalate)$", ErrorMessage = "Select a valid session decision.")]
    public string? Decision { get; set; }

    [StringLength(500, ErrorMessage = "Additional comments cannot exceed 500 characters.")]
    public string? AdditionalComments { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ConcernLevel == "Concerns" && string.IsNullOrWhiteSpace(AdditionalComments))
        {
            yield return new ValidationResult(
                "Describe the concerns before saving the review.",
                [nameof(AdditionalComments)]);
        }
    }
}
