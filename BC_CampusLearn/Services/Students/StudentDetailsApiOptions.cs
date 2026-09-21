namespace BC_CampusLearn.Services.Students;

public sealed class StudentDetailsApiOptions
{
    public const string SectionName = "StudentDetailsApi";

    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 10;
}
