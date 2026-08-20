using System.ComponentModel.DataAnnotations;
using BC_CampusLearn.Models.Entities;

namespace BC_CampusLearn.Models.ViewModels;

public class TutorPhoneNumberInput
{
    [Required(ErrorMessage = "Enter your phone number.")]
    [RegularExpression(@"^\d{10}$", ErrorMessage = "Enter a 10-digit phone number.")]
    [Display(Name = "Phone Number")]
    public string? PhoneNumber { get; set; }
}

public class TutorModuleChangeRequestInput
{
    [Required(ErrorMessage = "Select the change you want to request.")]
    [Display(Name = "Change type")]
    public TutorModuleChangeRequestType? RequestType { get; set; }

    [Required(ErrorMessage = "Select a module.")]
    [Range(1, int.MaxValue, ErrorMessage = "Select a module.")]
    [Display(Name = "Module")]
    public int? ProgrammeModuleId { get; set; }

    [Required(ErrorMessage = "Enter a reason for the module change.")]
    [StringLength(
        500,
        MinimumLength = 10,
        ErrorMessage = "Provide a reason between 10 and 500 characters.")]
    public string? Reason { get; set; }
}

public class TutorModuleOptionViewModel
{
    public int ProgrammeModuleId { get; set; }
    public string ModuleName { get; set; } = string.Empty;
}

public class TutorPendingModuleRequestViewModel
{
    public string ModuleName { get; set; } = string.Empty;
    public TutorModuleChangeRequestType RequestType { get; set; }
}
