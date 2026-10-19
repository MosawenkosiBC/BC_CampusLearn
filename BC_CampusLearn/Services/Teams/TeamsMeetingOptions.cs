namespace BC_CampusLearn.Services.Teams;

public sealed class TeamsMeetingOptions
{
    public const string SectionName = "TeamsMeetings";
    public bool Enabled { get; set; }
    public int[] PilotTutorBcUserIds { get; set; } = [];
    public int[] PilotTutorHeadBcUserIds { get; set; } = [];
    public bool SharedOrganizerEnabled { get; set; }
    public int[] SharedOrganizerPilotTutorBcUserIds { get; set; } = [2];
    public string SharedOrganizerEmail { get; set; } = "mzalaconnect@belgiumcampus.ac.za";
    public string? SharedOrganizerObjectId { get; set; }

    public bool AllowsTutor(int bcUserId) =>
        Enabled && PilotTutorBcUserIds.Contains(bcUserId);

    public bool AllowsTutorHead(int bcUserId) => Enabled && PilotTutorHeadBcUserIds.Contains(bcUserId);
}
