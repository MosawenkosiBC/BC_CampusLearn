namespace BC_CampusLearn.Authentication;

public record CurrentUser(
    int BcUserId,
    string PersonnelNumber,
    string ObjectId,
    string TenantId,
    string DisplayName,
    string? Email);
