using System.ComponentModel.DataAnnotations;
using BC_CampusLearn.Models.Entities;

namespace BC_CampusLearn.Models.ViewModels;

public class TutorOnboardingInput
{
    [Required(ErrorMessage = "Write a short bio to introduce yourself to students.")]
    [StringLength(500, ErrorMessage = "Your bio cannot exceed 500 characters.")]
    public string? Biography { get; set; }

    [RegularExpression(@"^\d{10}$", ErrorMessage = "Enter a 10-digit phone number.")]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "Choose your tutoring preference.")]
    [EnumDataType(typeof(PreferredTutoringMode))]
    public PreferredTutoringMode? PreferredTutoringMode { get; set; }
}
