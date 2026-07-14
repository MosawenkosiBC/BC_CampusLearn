using System.ComponentModel.DataAnnotations;

namespace BC_CampusLearn.Models.ViewModels;

public class CreateBookingInput
{
    [Required]
    public int TutorAvailabilityId { get; set; }

    [MaxLength(500)]
    [Display(Name = "Reason for the session")]
    public string? Reason { get; set; }
}