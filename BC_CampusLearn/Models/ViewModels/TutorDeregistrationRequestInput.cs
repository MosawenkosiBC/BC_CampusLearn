using System.ComponentModel.DataAnnotations;

namespace BC_CampusLearn.Models.ViewModels;

public class TutorDeregistrationRequestInput
{
    [Required(ErrorMessage = "Tell us why you want to leave the tutor programme.")]
    [StringLength(1000, MinimumLength = 10,
        ErrorMessage = "Provide a reason between 10 and 1000 characters.")]
    [Display(Name = "Reason for leaving")]
    public string? Reason { get; set; }

    [Required(ErrorMessage = "Type the confirmation phrase.")]
    [Display(Name = "Confirmation")]
    public string? ConfirmationText { get; set; }
}
