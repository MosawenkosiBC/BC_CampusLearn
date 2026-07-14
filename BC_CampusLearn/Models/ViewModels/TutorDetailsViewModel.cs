namespace BC_CampusLearn.Models.ViewModels;

public class TutorDetailsViewModel
{
    public int TutorId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Biography { get; set; } = string.Empty;

    public string? ProfileImagePath { get; set; }

    public List<string> Modules { get; set; } =
        new List<string>();

    public List<AvailabilitySlotViewModel> AvailabilitySlots
    { get; set; } =
        new List<AvailabilitySlotViewModel>();
}