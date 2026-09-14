using BC_CampusLearn.Models.Entities;

namespace BC_CampusLearn.Authentication;

public record CurrentUser(
    int BcUserId,
    string PersonnelNumber,
    string DisplayName,
    string? Email,
    BcUserRole Role = BcUserRole.Student);
