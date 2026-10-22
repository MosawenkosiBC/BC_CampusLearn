using BC_CampusLearn.Models.Entities;

namespace BC_CampusLearn.Services.Bookings;

public static class TutoringModeLocationPolicy
{
    public static string GetDeliveryMode(string studyAreaName) =>
        string.Equals(
            studyAreaName.Trim(),
            "Online",
            StringComparison.OrdinalIgnoreCase)
            ? "Online"
            : "Face-to-face";

    public static bool AllowsLocation(
        PreferredTutoringMode tutoringMode,
        string studyAreaName)
    {
        bool isOnline = GetDeliveryMode(studyAreaName) == "Online";

        return tutoringMode switch
        {
            PreferredTutoringMode.Online => isOnline,
            PreferredTutoringMode.FaceToFace => !isOnline,
            PreferredTutoringMode.Both => true,
            _ => false
        };
    }
}
