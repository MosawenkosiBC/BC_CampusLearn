namespace BC_CampusLearn.Authentication;

public static class EntraClaimTypes
{
    public const string ObjectId = "oid";

    public const string TenantId = "tid";

    public const string DisplayName = "name";

    public const string PreferredUsername = "preferred_username";
    public const string BcUserId = "bc_user_id";
    public const string PersonnelNumber = "personnel_number";

    public const string ObjectIdUri =
        "http://schemas.microsoft.com/identity/claims/objectidentifier";

    public const string TenantIdUri =
        "http://schemas.microsoft.com/identity/claims/tenantid";
}
