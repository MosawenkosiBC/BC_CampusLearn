using System.Security.Claims;

using BC_CampusLearn.Models.Entities;

namespace BC_CampusLearn.Authentication;

public class ClaimsCurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ClaimsCurrentUserService(
        IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal =>
        _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated =>
        Principal?.Identity?.IsAuthenticated == true;

    public CurrentUser GetRequiredUser()
    {
        ClaimsPrincipal principal = Principal
            ?? throw new UnauthorizedAccessException(
                "No authenticated user was found.");

        if (principal.Identity?.IsAuthenticated != true)
        {
            throw new UnauthorizedAccessException(
                "The user is not authenticated.");
        }

        string? bcUserIdValue =
            principal.FindFirstValue(EntraClaimTypes.BcUserId);
        string? personnelNumber =
            principal.FindFirstValue(EntraClaimTypes.PersonnelNumber);

        if (!int.TryParse(bcUserIdValue, out int bcUserId) ||
            string.IsNullOrWhiteSpace(personnelNumber))
        {
            throw new InvalidOperationException(
                "The authenticated principal has not been linked to a BC user.");
        }

        string displayName =
            principal.FindFirstValue(ClaimTypes.Name)
            ?? principal.FindFirstValue(
                EntraClaimTypes.DisplayName)
            ?? "Unknown student";

        string? email =
            principal.FindFirstValue(ClaimTypes.Email)
            ?? principal.FindFirstValue(
                EntraClaimTypes.PreferredUsername);

        string? roleValue = principal.FindFirstValue(EntraClaimTypes.BcRole);
        if (!Enum.TryParse(roleValue, ignoreCase: false, out BcUserRole role) ||
            !Enum.IsDefined(role))
        {
            throw new InvalidOperationException(
                "The authenticated principal does not have a valid BC user role.");
        }




        return new CurrentUser(
            bcUserId,
            personnelNumber,
            displayName,
            email,
            role);
    }
}
