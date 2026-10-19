namespace BC_CampusLearn.Services.Teams;

public sealed class TeamsMeetingOptions
{
    public const string SectionName = "TeamsMeetings";
    public bool Enabled { get; set; }

    // Meeting creation is enabled for all eligible tutors. Reading meeting
    // evidence remains scoped to explicitly configured pilot organisers.
    public int[] PilotTutorBcUserIds { get; set; } = [];

    public bool AllowsTutor(int bcUserId) =>
        Enabled && PilotTutorBcUserIds.Contains(bcUserId);
}
