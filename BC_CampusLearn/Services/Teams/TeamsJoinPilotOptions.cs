namespace BC_CampusLearn.Services.Teams;

public sealed class TeamsJoinPilotOptions
{
    public const string SectionName = "TeamsJoinPilot";
    public const string WebhookPath = "/api/teams/meeting-events";
    // Deliberately fixed for this pilot. Legacy TeamsMeetings allowlists do not apply.
    public const int PilotBcUserId = 2;
    public bool Enabled { get; set; }
    public string NotificationUrl { get; set; } = "https://tutors.belgiumcampus.ac.za" + WebhookPath;
    public string CertificatePath { get; set; } = "";
    public string CertificatePassword { get; set; } = "";
    public string ClientStateKey { get; set; } = "";

    public bool IsConfigured => Enabled &&
        Uri.TryCreate(NotificationUrl, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps && uri.AbsolutePath == WebhookPath &&
        string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.UserInfo) &&
        Path.IsPathFullyQualified(CertificatePath) && File.Exists(CertificatePath) &&
        !string.IsNullOrWhiteSpace(CertificatePassword) && HasStateKey();

    private bool HasStateKey()
    {
        try { return Convert.FromBase64String(ClientStateKey).Length >= 32; }
        catch (FormatException) { return false; }
    }
}
