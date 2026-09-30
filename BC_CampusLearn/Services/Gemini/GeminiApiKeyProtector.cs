using Microsoft.AspNetCore.DataProtection;

namespace BC_CampusLearn.Services.Gemini;

public sealed class GeminiApiKeyProtector : IGeminiApiKeyProtector
{
    private const string Purpose = "BC_CampusLearn.GeminiApiKey.v1";
    private readonly IDataProtector _protector;

    public GeminiApiKeyProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public string Protect(string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        return _protector.Protect(apiKey.Trim());
    }

    public string Unprotect(string protectedApiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedApiKey);
        return _protector.Unprotect(protectedApiKey);
    }
}
