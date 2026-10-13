namespace BC_CampusLearn.Services.Teams;

public sealed class TeamsMeetingOptions
{
    public const string SectionName = "TeamsMeetings";
    public bool Enabled { get; set; }
    public int[] PilotTutorBcUserIds { get; set; } = [];

    public bool AllowsTutor(int bcUserId) =>
        Enabled && PilotTutorBcUserIds.Contains(bcUserId);
}
