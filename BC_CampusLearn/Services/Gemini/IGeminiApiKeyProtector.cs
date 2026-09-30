namespace BC_CampusLearn.Services.Gemini;

public interface IGeminiApiKeyProtector
{
    string Protect(string apiKey);

    string Unprotect(string protectedApiKey);
}
