using System.ComponentModel.DataAnnotations;

namespace BC_CampusLearn.Models.ViewModels;

public class TutorApplicationStageOneInput
{
    [Required(ErrorMessage = "Enter your phone number.")]
    [RegularExpression(
        @"^\d{10}$",
        ErrorMessage = "Enter a 10-digit phone number.")]
    [Display(Name = "Phone Number")]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "Enter your academic average.")]
    [Range(
        65,
        100,
        ErrorMessage = "Academic average must be between 65 and 100.")]
    [Display(Name = "Academic Average (%)")]
    public decimal? OverallAverage { get; set; }

    [Required(ErrorMessage = "Select your year of study.")]
    [Range(1, 4, ErrorMessage = "Select a valid year of study.")]
    [Display(Name = "Year of Study")]
    public int? YearOfStudy { get; set; }

    [Required(ErrorMessage = "Select your programme.")]
    [Range(1, int.MaxValue, ErrorMessage = "Select your programme.")]
    [Display(Name = "Program")]
    public int? ProgrammeId { get; set; }
}
