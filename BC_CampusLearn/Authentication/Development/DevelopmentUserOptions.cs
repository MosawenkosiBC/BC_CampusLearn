namespace BC_CampusLearn.Authentication.Development;

public class DevelopmentUserOptions
{
    public const string SectionName = "DevelopmentUser";

    public string ObjectId { get; set; } = string.Empty;

    public string TenantId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
    public string PersonnelNumber { get; set; } = string.Empty;
}
