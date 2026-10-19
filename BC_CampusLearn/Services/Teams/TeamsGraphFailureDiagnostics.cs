using System.Text.Json;
using System.Text.RegularExpressions;

namespace BC_CampusLearn.Services.Teams;

public sealed record TeamsGraphFailureDiagnostics(string Code, string? RequestId, bool MissingAccessPolicy)
{
    public static async Task<TeamsGraphFailureDiagnostics> ReadAsync(HttpResponseMessage response, CancellationToken ct)
    {
        string code = "unknown";
        string? requestId = response.Headers.TryGetValues("request-id", out var values)
            && Guid.TryParse(values.FirstOrDefault(), out var requestGuid) ? requestGuid.ToString("D") : null;
        bool missingPolicy = false;
        try
        {
            // Parse only bounded error metadata. Never retain/log Microsoft's
            // message or raw body, which can contain identifiers or other data.
            await response.Content.LoadIntoBufferAsync(64 * 1024, ct);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (json.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
            {
                if (error.TryGetProperty("code", out var errorCode) && errorCode.ValueKind == JsonValueKind.String)
                {
                    string candidate = errorCode.GetString() ?? "";
                    if (Regex.IsMatch(candidate, @"\A[a-zA-Z0-9_.-]{1,80}\z")) code = candidate;
                }
                if (error.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                    missingPolicy = message.GetString()?.Contains("No application access policy found", StringComparison.OrdinalIgnoreCase) == true;
                if (requestId is null && error.TryGetProperty("innerError", out var inner) && inner.ValueKind == JsonValueKind.Object &&
                    inner.TryGetProperty("request-id", out var innerId) && innerId.ValueKind == JsonValueKind.String &&
                    Guid.TryParse(innerId.GetString(), out var parsed)) requestId = parsed.ToString("D");
            }
        }
        catch (Exception ex) when (ex is JsonException or HttpRequestException or IOException or InvalidOperationException)
        {
            // Keep the HTTP status useful even when the error body cannot be read.
        }
        return new(code, requestId, missingPolicy);
    }
}
