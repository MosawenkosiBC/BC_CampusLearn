using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace BC_CampusLearn.Authentication;

public static class TutorOnboardingSession
{
    public const string ClaimType = "tutor_onboarding_login_id";

    public static void Configure(CookieAuthenticationOptions options)
    {
        var previous = options.Events.OnSigningIn;
        options.Events.OnSigningIn = async context =>
        {
            await previous(context);
            if (context.Principal?.Identity is not ClaimsIdentity identity) return;
            foreach (var claim in identity.FindAll(ClaimType).ToList())
                identity.RemoveClaim(claim);
            // Cookie renewal retains this value; a fresh login creates a new one.
            identity.AddClaim(new Claim(ClaimType, Guid.NewGuid().ToString("N")));
        };
    }
}
