using BC_CampusLearn.Models.Entities;

namespace BC_CampusLearn.Services.Bookings;

public static class TutoringModeLocationPolicy
{
    public static bool AllowsLocation(
        PreferredTutoringMode tutoringMode,
        string studyAreaName)
    {
        bool isOnline = string.Equals(
            studyAreaName.Trim(),
            "Online",
            StringComparison.OrdinalIgnoreCase);

        return tutoringMode switch
        {
            PreferredTutoringMode.Online => isOnline,
            PreferredTutoringMode.FaceToFace => !isOnline,
            PreferredTutoringMode.Both => true,
            _ => false
        };
    }
}
