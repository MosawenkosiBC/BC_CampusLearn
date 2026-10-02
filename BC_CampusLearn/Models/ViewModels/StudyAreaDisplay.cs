namespace BC_CampusLearn.Models.ViewModels;

public static class StudyAreaDisplay
{
    public static string GetSubtext(string studyAreaName) =>
        string.Equals(
            studyAreaName,
            "Online",
            StringComparison.OrdinalIgnoreCase)
            ? "Microsoft Teams"
            : "Pretoria Campus";
}
