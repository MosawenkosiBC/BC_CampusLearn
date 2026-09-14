namespace BC_CampusLearn.Authentication;

public record CurrentUser(
    int BcUserId,
    string PersonnelNumber,
    string DisplayName,
    string? Email);
