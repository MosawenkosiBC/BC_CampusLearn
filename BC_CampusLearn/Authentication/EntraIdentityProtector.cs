using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace BC_CampusLearn.Authentication;

public sealed class EntraIdentityProtector : IEntraIdentityProtector
{
    private const string Purpose =
        "BC_CampusLearn.EntraIdentity.v1";

    private readonly IDataProtector _protector;
    private readonly byte[] _lookupKey;

    public EntraIdentityProtector(
        IDataProtectionProvider provider,
        IOptions<IdentityProtectionOptions> options)
    {
        _protector = provider.CreateProtector(Purpose);
        if (!options.Value.TryGetLookupKey(out _lookupKey))
        {
            throw new InvalidOperationException(
                "IdentityProtection:LookupKey must be a Base64-encoded key of at least 32 bytes.");
        }
    }

    public ProtectedEntraIdentity Protect(string tenantId, string objectId)
    {
        string normalizedTenantId = NormalizeGuid(tenantId, "tenant ID");
        string normalizedObjectId = NormalizeGuid(objectId, "object ID");

        return new ProtectedEntraIdentity(
            _protector.Protect(normalizedTenantId),
            _protector.Protect(normalizedObjectId),
            CreateLookupHash(normalizedTenantId, normalizedObjectId));
    }

    public string CreateLookupHash(string tenantId, string objectId)
    {
        string normalizedTenantId = NormalizeGuid(tenantId, "tenant ID");
        string normalizedObjectId = NormalizeGuid(objectId, "object ID");
        byte[] identityBytes = Encoding.UTF8.GetBytes(
            $"{normalizedTenantId}:{normalizedObjectId}");

        return Convert.ToHexString(HMACSHA256.HashData(
            _lookupKey,
            identityBytes));
    }

    private static string NormalizeGuid(string value, string description)
    {
        if (!Guid.TryParse(value, out Guid parsed))
        {
            throw new InvalidOperationException(
                $"The authenticated principal does not contain a valid Entra {description}.");
        }

        return parsed.ToString("D");
    }
}
