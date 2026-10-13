using System.Text.RegularExpressions;
using Microsoft.Identity.Client;

namespace BC_CampusLearn.Services.Teams;

public sealed record TeamsAuthenticationDiagnostics(
    string ErrorCode, string? EntraErrorCode, string? CorrelationId, int? StatusCode, string UserMessage)
{
    public static TeamsAuthenticationDiagnostics FromException(MsalException exception)
    {
        // Extract only bounded diagnostic codes. Never expose/log the exception
        // message or response body, which may contain identifiers or credentials.
        string errorCode = Regex.IsMatch(exception.ErrorCode ?? string.Empty, @"\A[a-zA-Z0-9_]{1,80}\z")
            ? exception.ErrorCode! : "unknown";
        Match entraMatch = Regex.Match(exception.Message, @"\bAADSTS[0-9]{5,8}\b");
        string? entraCode = entraMatch.Success ? entraMatch.Value : null;
        string? correlationId = Guid.TryParse(exception.CorrelationId, out Guid correlation)
            ? correlation.ToString("D") : null;
        int? status = exception is MsalServiceException service && service.StatusCode > 0 ? service.StatusCode : null;

        string message = entraCode switch
        {
            "AADSTS7000215" => "Microsoft rejected the application client secret. Ask the administrator to check AzureAd:ClientSecret uses the secret value, not its secret ID, from the configured app registration.",
            "AADSTS7000222" => "The Microsoft application client secret has expired. Ask the administrator to update it and recreate the application container.",
            "AADSTS700016" => "Microsoft could not find the application in the configured tenant. Ask the administrator to check AzureAd:ClientId and AzureAd:TenantId.",
            "AADSTS90002" => "Microsoft could not find the configured tenant. Ask the administrator to check AzureAd:TenantId.",
            "AADSTS7000112" => "The Microsoft application is disabled. Please contact the administrator.",
            _ when errorCode == "http_request_failed" => "CampusLearn could not reach Microsoft's authentication service. Ask the administrator to check the application container's network access.",
            _ => "CampusLearn could not authenticate with Microsoft. Please ask the administrator to check the application authentication logs."
        };

        return new(errorCode, entraCode, correlationId, status,
            $"{message} Reference: {entraCode ?? errorCode}.");
    }
}
