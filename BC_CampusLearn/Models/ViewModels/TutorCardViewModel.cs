namespace BC_CampusLearn.Models.ViewModels;

public class TutorCardViewModel
{
    public int TutorId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string Biography { get; set; } = string.Empty;

    public string? ProfileImagePath { get; set; }

    public int ProgrammeId { get; set; }

    public string ProgrammeName { get; set; } = string.Empty;

    public int YearOfStudy { get; set; }

    public int UpcomingAvailabilityCount { get; set; }

    public DateTimeOffset? NextAvailableAt { get; set; }

    public List<string> Modules { get; set; } =
        new List<string>();

    public List<string> ModuleCodes { get; set; } =
        new List<string>();
}
