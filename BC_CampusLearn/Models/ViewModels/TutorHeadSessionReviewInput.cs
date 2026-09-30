using System.ComponentModel.DataAnnotations;

namespace BC_CampusLearn.Models.ViewModels;

public class TutorHeadSessionReviewInput
{
    [Required(ErrorMessage = "Select whether the correct module and topic were covered.")]
    [RegularExpression("^(Yes|Partially|No)$", ErrorMessage = "Select a valid module and topic response.")]
    public string? ModuleAndTopicCoverage { get; set; }

    [Required(ErrorMessage = "Select how clear the tutor's explanations were.")]
    [RegularExpression("^(Excellent|Good|Satisfactory|Needs improvement)$", ErrorMessage = "Select a valid explanation clarity response.")]
    public string? ExplanationClarity { get; set; }

    [Required(ErrorMessage = "Select whether the session was structured effectively.")]
    [RegularExpression("^(Yes|Partially|No)$", ErrorMessage = "Select a valid session structure response.")]
    public string? SessionStructure { get; set; }

    [Required(ErrorMessage = "Select whether the tutor engaged the student appropriately.")]
    [RegularExpression("^(Yes|Partially|No)$", ErrorMessage = "Select a valid student engagement response.")]
    public string? StudentEngagement { get; set; }

    [Required(ErrorMessage = "Select whether the recording and submitted reviews match.")]
    [RegularExpression("^(Yes|Partially|No)$", ErrorMessage = "Select a valid recording and review response.")]
    public string? EvidenceConsistency { get; set; }

    [Required(ErrorMessage = "Select whether any concerns were identified.")]
    [RegularExpression("^(No concerns|Minor concerns|Serious concerns)$", ErrorMessage = "Select a valid concern level.")]
    public string? ConcernLevel { get; set; }

    [Required(ErrorMessage = "Select an overall session assessment.")]
    [RegularExpression("^(Excellent|Good|Satisfactory|Needs improvement)$", ErrorMessage = "Select a valid overall assessment.")]
    public string? OverallAssessment { get; set; }

    [Required(ErrorMessage = "Select a decision for this session.")]
    [RegularExpression("^(Approve|Approve with feedback|Request clarification|Escalate)$", ErrorMessage = "Select a valid session decision.")]
    public string? Decision { get; set; }

    [StringLength(500, ErrorMessage = "Additional comments cannot exceed 500 characters.")]
    public string? AdditionalComments { get; set; }
}
