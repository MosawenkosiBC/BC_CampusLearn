using System.ComponentModel.DataAnnotations;

namespace BC_CampusLearn.Models.ViewModels;

public class TutorApplicationStageTwoInput
{
    [Required(ErrorMessage = "Tell us why you want to become a tutor.")]
    [MaxLength(1000)]
    [Display(Name = "Why do you want to become a tutor?")]
    public string? ReasonForTutoring { get; set; }

    [Required(ErrorMessage = "Describe your teaching style.")]
    [MaxLength(1000)]
    [Display(Name = "How would you describe your teaching style?")]
    public string? TeachingStyle { get; set; }

    [Required(ErrorMessage = "Describe your previous tutoring experience.")]
    [MaxLength(1000)]
    [Display(Name = "Previous Tutoring Experience")]
    public string? PreviousTutoringExperience { get; set; }

    [Required(ErrorMessage = "Select your campus of study.")]
    [MaxLength(100)]
    [Display(Name = "Campus of Study")]
    public string? CampusOfStudy { get; set; }

    [Required(ErrorMessage = "Add your demonstration link.")]
    [Url(ErrorMessage = "Enter a valid demonstration URL.")]
    [MaxLength(500)]
    [Display(Name = "Demonstration Link")]
    public string? DemonstrationVideoUrl { get; set; }

}
