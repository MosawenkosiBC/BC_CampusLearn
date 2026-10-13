using Microsoft.Identity.Client;

namespace BC_CampusLearn.Services.Teams;

public interface ITeamsGraphTokenProvider
{
    Task<string> GetTokenAsync(CancellationToken cancellationToken);
}

public sealed class TeamsGraphTokenProvider(
    IConfiguration configuration,
    ILogger<TeamsGraphTokenProvider>? logger = null)
    : ITeamsGraphTokenProvider
{
    private readonly object _sync = new();
    private IConfidentialClientApplication? _application;

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        IConfidentialClientApplication application;
        lock (_sync)
        {
            if (_application is null)
            {
                string? tenant = configuration["AzureAd:TenantId"];
                string? client = configuration["AzureAd:ClientId"];
                string? secret = configuration["AzureAd:ClientSecret"];
                if (!Guid.TryParse(tenant, out Guid tenantId) ||
                    !Guid.TryParse(client, out Guid clientId) ||
                    string.IsNullOrWhiteSpace(secret))
                {
                    throw new TeamsMeetingException(
                        "Teams meeting creation is not configured. Please contact the administrator.");
                }

                _application = ConfidentialClientApplicationBuilder
                    .Create(clientId.ToString("D"))
                    .WithAuthority($"https://login.microsoftonline.com/{tenantId:D}")
                    .WithClientSecret(secret)
                    .Build();
            }
            application = _application;
        }

        try
        {
            // MSAL caches application tokens; no tutor access token is required.
            AuthenticationResult result = await application
                .AcquireTokenForClient(["https://graph.microsoft.com/.default"])
                .ExecuteAsync(cancellationToken);
            return result.AccessToken;
        }
        catch (MsalException exception)
        {
            TeamsAuthenticationDiagnostics failure = TeamsAuthenticationDiagnostics.FromException(exception);
            logger?.LogWarning(
                "Microsoft Graph application authentication failed. MSAL error {ErrorCode}; Entra error {EntraErrorCode}; HTTP status {StatusCode}; correlation {CorrelationId}.",
                failure.ErrorCode, failure.EntraErrorCode, failure.StatusCode, failure.CorrelationId);
            throw new TeamsMeetingException(failure.UserMessage);
        }
    }
}
