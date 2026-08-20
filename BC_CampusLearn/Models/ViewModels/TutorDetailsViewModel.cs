namespace BC_CampusLearn.Models.ViewModels;

public class TutorDetailsViewModel
{
    public int TutorId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Biography { get; set; } = string.Empty;

    public string? ProfileImagePath { get; set; }

    public string Initials { get; set; } = string.Empty;

    public string? LinkedInUrl { get; set; }

    public string? GitHubUrl { get; set; }

    public List<BookingModuleOptionViewModel> Modules { get; set; } =
        new List<BookingModuleOptionViewModel>();

    public List<AvailabilitySlotViewModel> AvailabilitySlots
    { get; set; } =
        new List<AvailabilitySlotViewModel>();
}
