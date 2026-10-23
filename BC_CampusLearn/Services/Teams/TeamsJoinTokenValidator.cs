using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace BC_CampusLearn.Services.Teams;

public interface ITeamsJoinTokenValidator
{
    Task<bool> ValidateAsync(string token, CancellationToken ct);
}

public sealed class TeamsJoinTokenValidator(IConfiguration configuration,
    IConfigurationManager<OpenIdConnectConfiguration>? configurationManager = null) : ITeamsJoinTokenValidator
{
    private readonly IConfigurationManager<OpenIdConnectConfiguration> metadata = configurationManager ?? new ConfigurationManager<OpenIdConnectConfiguration>(
        "https://login.microsoftonline.com/common/v2.0/.well-known/openid-configuration",
        new OpenIdConnectConfigurationRetriever());

    public async Task<bool> ValidateAsync(string token, CancellationToken ct)
    {
        if (!Guid.TryParse(configuration["AzureAd:TenantId"], out var tenant) ||
            !Guid.TryParse(configuration["AzureAd:ClientId"], out var app)) return false;
        var keys = await metadata.GetConfigurationAsync(ct);
        try
        {
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true,
                ValidateIssuerSigningKey = true, RequireSignedTokens = true,
                ValidIssuers = [$"https://sts.windows.net/{tenant:D}/", $"https://login.microsoftonline.com/{tenant:D}/v2.0"],
                ValidAudience = app.ToString("D"), IssuerSigningKeys = keys.SigningKeys,
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256], ClockSkew = TimeSpan.FromMinutes(1)
            }, out _);
            var callerClaim = principal.FindFirst("ver")?.Value switch
            {
                "1.0" => "appid", "2.0" => "azp", _ => ""
            };
            return principal.FindFirst("tid")?.Value == tenant.ToString("D") &&
                principal.FindFirst(callerClaim)?.Value == "0bf30f3b-4a52-48df-9a82-234910c4a086";
        }
        catch (SecurityTokenSignatureKeyNotFoundException) { metadata.RequestRefresh(); return false; }
        catch (SecurityTokenException) { return false; }
        catch (ArgumentException) { return false; }
    }
}
