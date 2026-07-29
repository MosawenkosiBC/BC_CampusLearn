using System.ComponentModel.DataAnnotations;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Validation;
using Microsoft.AspNetCore.Http;

namespace BC_CampusLearn.Models.ViewModels;

public class CreateBookingInput
{
    [Required]
    public int TutorAvailabilityId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Select a module.")]
    [Display(Name = "Module")]
    public int ProgrammeModuleId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Location { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter a session summary.")]
    [MinLength(
        75,
        ErrorMessage =
            "The session summary must be at least 75 characters.")]
    [MaxLength(
        1000,
        ErrorMessage =
            "The session summary cannot exceed 1,000 characters.")]
    [Display(Name = "Session summary")]
    public string? Summary { get; set; }

    public List<string?> PreparationLinks { get; set; }
        = new() { null, null, null };

    public List<IFormFile> Documents { get; set; } = new();

    [MustBeTrue(
        ErrorMessage = "You must agree to the terms and conditions.")]
    public bool AcceptedTerms { get; set; }
}
