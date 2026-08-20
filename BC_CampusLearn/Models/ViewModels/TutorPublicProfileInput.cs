using System.ComponentModel.DataAnnotations;
using BC_CampusLearn.Models.Entities;

namespace BC_CampusLearn.Models.ViewModels;

public class TutorPublicProfileInput
{
    [MaxLength(500, ErrorMessage = "Biography cannot exceed 500 characters.")]
    public string? Biography { get; set; }

    [Required(ErrorMessage = "Select your tutoring preference.")]
    [EnumDataType(
        typeof(PreferredTutoringMode),
        ErrorMessage = "Select a valid tutoring preference.")]
    [Display(Name = "Tutoring preference")]
    public PreferredTutoringMode? PreferredTutoringMode { get; set; }

    [Display(Name = "GitHub URL")]
    [MaxLength(500, ErrorMessage = "GitHub URL cannot exceed 500 characters.")]
    public string? GitHubUrl { get; set; }

    [Display(Name = "LinkedIn URL")]
    [MaxLength(500, ErrorMessage = "LinkedIn URL cannot exceed 500 characters.")]
    public string? LinkedInUrl { get; set; }
}
