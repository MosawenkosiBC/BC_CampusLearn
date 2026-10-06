using System.ComponentModel.DataAnnotations;

namespace BC_CampusLearn.Models.ViewModels;

public class AdminSessionReviewInput
{
    [Required(ErrorMessage = "Choose Yes or No for this question.")]
    public bool? HeadConfirmedQuality { get; set; }

    [Required(ErrorMessage = "Choose Yes or No for this question.")]
    public bool? ReviewEvidenceIsConsistent { get; set; }

    [Required(ErrorMessage = "Choose Yes or No for this question.")]
    public bool? ConcernsResolvedOrDocumented { get; set; }

    [Required(ErrorMessage = "Choose Approve or Decline.")]
    public bool? EvidenceSupportsApproval { get; set; }
}
