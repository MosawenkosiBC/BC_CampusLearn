using System.ComponentModel.DataAnnotations;
using BC_CampusLearn.Models.Entities;
using Microsoft.AspNetCore.Http;

namespace BC_CampusLearn.Models.ViewModels;

public class TutorApplicationStageThreeInput
{
    [Required(ErrorMessage = "Select your preferred tutoring mode.")]
    [Display(Name = "Preferred Tutoring Mode")]
    public PreferredTutoringMode? PreferredTutoringMode { get; set; }

    [Required(ErrorMessage = "Attach your academic transcript.")]
    [Display(Name = "Transcript")]
    public IFormFile? Transcript { get; set; }

    [Display(Name = "Additional Certificate")]
    public IFormFile? AdditionalCertificate { get; set; }

    [Required(ErrorMessage = "Select at least two modules.")]
    [MinLength(2, ErrorMessage = "Select at least two modules.")]
    [MaxLength(5, ErrorMessage = "Select no more than five modules.")]
    [Display(Name = "Subjects You Can Tutor")]
    public List<int> ProgrammeModuleIds { get; set; } = new();
}
