using System.ComponentModel.DataAnnotations;
using BC_CampusLearn.Models.Entities;

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

    [MaxLength(1000)]
    [Display(Name = "Session summary")]
    public string? Summary { get; set; }

    public List<string?> PreparationLinks { get; set; }
        = new() { null, null, null };
}
