namespace BC_CampusLearn.Models.ViewModels;

public static class StudyAreaDisplay
{
    public const string PretoriaCampus = "Pretoria Campus";
    public const string MicrosoftTeams = "Microsoft Teams";

    public static string GetSubtext(string studyAreaName) =>
        string.Equals(
            studyAreaName,
            "Online",
            StringComparison.OrdinalIgnoreCase)
            ? MicrosoftTeams
            : PretoriaCampus;

    public static bool IsCampusLabel(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 80;
}
