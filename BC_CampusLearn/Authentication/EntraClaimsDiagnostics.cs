using System.Security.Claims;
using System.Text.Json;

namespace BC_CampusLearn.Authentication;

public static class EntraClaimsDiagnostics
{
    private static readonly HashSet<string> RedactedClaimTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "aio",
            "at_hash",
            "c_hash",
            "nonce",
            "rh",
            "uti"
        };

    public static string CreateJson(ClaimsPrincipal principal)
    {
        var claims = principal.Claims
            .OrderBy(claim => claim.Type, StringComparer.Ordinal)
            .ThenBy(claim => claim.Value, StringComparer.Ordinal)
            .Select(claim => new
            {
                type = claim.Type,
                value = RedactedClaimTypes.Contains(claim.Type)
                    ? "[redacted]"
                    : claim.Value
            });

        return JsonSerializer.Serialize(
            claims,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });
    }
}
