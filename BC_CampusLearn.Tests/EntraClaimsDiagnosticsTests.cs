using System.Security.Claims;
using BC_CampusLearn.Authentication;
using Xunit;

namespace BC_CampusLearn.Tests;

public sealed class EntraClaimsDiagnosticsTests
{
    [Fact]
    public void CreateJson_IncludesBusinessClaimsAndRedactsProtocolClaims()
    {
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(
            [
                new Claim("name", "Test Administrator"),
                new Claim("personnel_number", "P12345"),
                new Claim("nonce", "secret-nonce")
            ],
            "Test"));

        string json = EntraClaimsDiagnostics.CreateJson(principal);

        Assert.Contains("Test Administrator", json);
        Assert.Contains("P12345", json);
        Assert.Contains("[redacted]", json);
        Assert.DoesNotContain("secret-nonce", json);
    }
}
