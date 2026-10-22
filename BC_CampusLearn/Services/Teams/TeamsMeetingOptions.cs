namespace BC_CampusLearn.Services.Teams;

public sealed class TeamsMeetingOptions
{
    public const string SectionName = "TeamsMeetings";
    public bool Enabled { get; set; }

    // Legacy pilot lists remain bindable so older deployments can be upgraded
    // without configuration errors. Teams access is no longer allowlisted.
    public int[] PilotTutorBcUserIds { get; set; } = [];
    public bool SharedOrganizerEnabled { get; set; }
    public int[] SharedOrganizerPilotTutorBcUserIds { get; set; } = [];
    public string SharedOrganizerEmail { get; set; } = "mzalaconnect@belgiumcampus.ac.za";
    public string? SharedOrganizerObjectId { get; set; }

    public int[] PilotTutorHeadBcUserIds { get; set; } = [];

    public bool AllowsTutor(int bcUserId) => Enabled;

    public bool AllowsTutorHead(int bcUserId) => Enabled;
}
