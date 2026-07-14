namespace BC_CampusLearn.Models.ViewModels;

public class TutorCardViewModel
{
    public int TutorId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string Biography { get; set; } = string.Empty;

    public string? ProfileImagePath { get; set; }

    public List<string> Modules { get; set; } =
        new List<string>();
}