using System.Security.Claims;

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

        string? objectId =
            principal.FindFirstValue(EntraClaimTypes.ObjectId)
            ?? principal.FindFirstValue(
                EntraClaimTypes.ObjectIdUri);

        string? tenantId =
            principal.FindFirstValue(EntraClaimTypes.TenantId)
            ?? principal.FindFirstValue(
                EntraClaimTypes.TenantIdUri);

        if (string.IsNullOrWhiteSpace(objectId))
        {
            throw new InvalidOperationException(
                "The authenticated user has no Entra object ID.");
        }

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new InvalidOperationException(
                "The authenticated user has no Entra tenant ID.");
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




        return new CurrentUser(
            objectId,
            tenantId,
            displayName,
            email);
    }
}