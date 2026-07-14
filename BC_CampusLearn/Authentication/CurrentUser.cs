namespace BC_CampusLearn.Authentication;

public record CurrentUser(
    string ObjectId,
    string TenantId,
    string DisplayName,
    string? Email);