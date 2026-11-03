using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace BC_CampusLearn.Services.Teams;

public sealed class TeamsJoinPilotCrypto(IOptions<TeamsJoinPilotOptions> options)
{
    public X509Certificate2 LoadCertificate()
    {
        var certificate = X509CertificateLoader.LoadPkcs12FromFile(
            options.Value.CertificatePath, options.Value.CertificatePassword,
            X509KeyStorageFlags.EphemeralKeySet);
        using var rsa = certificate.GetRSAPrivateKey();
        if (rsa is null || rsa.KeySize < 2048 || rsa.KeySize > 4096 ||
            certificate.NotAfter.ToUniversalTime() <= DateTime.UtcNow ||
            certificate.NotBefore.ToUniversalTime() > DateTime.UtcNow)
        {
            certificate.Dispose();
            throw new CryptographicException("The Teams join certificate is invalid or expired.");
        }
        return certificate;
    }

    public static string Resource(string joinUrl) =>
        $"communications/onlineMeetings(joinWebUrl='{Uri.EscapeDataString(joinUrl)}')/meetingCallEvents";

    public static string NormalizeResource(string resource)
    {
        resource = resource.TrimStart('/');
        const string prefix = "communications/onlineMeetings(joinWebUrl='";
        const string suffix = "')/meetingCallEvents";
        if (!resource.StartsWith(prefix, StringComparison.Ordinal) ||
            !resource.EndsWith(suffix, StringComparison.Ordinal)) return resource;
        var url = resource[prefix.Length..^suffix.Length];
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) url = Uri.UnescapeDataString(url);
        return Resource(url);
    }

    public string ClientState(int bookingId, string resource) =>
        $"mzala-join-v1:{bookingId}:" + Convert.ToHexString(HMACSHA256.HashData(
            Convert.FromBase64String(options.Value.ClientStateKey),
            Encoding.UTF8.GetBytes($"{bookingId}:{NormalizeResource(resource)}")));

    // Graph deliberately redacts clientState from subscription GET/list responses.
    // A non-secret namespace in certificateId lets reconciliation recover ownership.
    private string Owner => Convert.ToHexString(HMACSHA256.HashData(
        Convert.FromBase64String(options.Value.ClientStateKey), Encoding.UTF8.GetBytes("mzala-join-subscription-owner-v1")))[..16];

    public string CertificateId(string thumbprint, int bookingId) => $"{thumbprint}:mzala-join-v1:{bookingId}:{Owner}";

    public bool TryGetOwnedBooking(string? certificateId, out int bookingId)
    {
        bookingId = 0;
        var parts = certificateId?.Split(':');
        return parts is { Length: 4 } && parts[1] == "mzala-join-v1" && parts[3] == Owner &&
            int.TryParse(parts[2], out bookingId) && bookingId > 0;
    }

    public bool TryValidateState(string? state, string resource, out int bookingId)
    {
        bookingId = 0;
        var parts = state?.Split(':');
        if (parts is not { Length: 3 } || parts[0] != "mzala-join-v1" ||
            !int.TryParse(parts[1], out bookingId) || bookingId <= 0) return false;
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(state!), Encoding.UTF8.GetBytes(ClientState(bookingId, resource)));
    }

    public JsonDocument Decrypt(JsonElement content, int bookingId)
    {
        using var certificate = LoadCertificate();
        if (content.GetProperty("encryptionCertificateId").GetString() != CertificateId(certificate.Thumbprint, bookingId))
            throw new CryptographicException("Unknown Teams notification certificate.");
        using var rsa = certificate.GetRSAPrivateKey()!;
        byte[] key = rsa.Decrypt(Convert.FromBase64String(content.GetProperty("dataKey").GetString()!),
            RSAEncryptionPadding.OaepSHA1);
        try
        {
            byte[] data = Convert.FromBase64String(content.GetProperty("data").GetString()!);
            byte[] signature = Convert.FromBase64String(content.GetProperty("dataSignature").GetString()!);
            if (!CryptographicOperations.FixedTimeEquals(HMACSHA256.HashData(key, data), signature))
                throw new CryptographicException("Invalid Teams notification signature.");
            using var aes = Aes.Create();
            aes.Key = key;
            return JsonDocument.Parse(aes.DecryptCbc(data, key.AsSpan(0, 16), PaddingMode.PKCS7));
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }
}
